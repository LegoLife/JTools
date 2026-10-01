using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static TextExpander.NativeMethods;

namespace TextExpander;

/// <summary>
/// Watches keystrokes system-wide and feeds the ones that type into a <see cref="TypedWordTracker"/>,
/// which decides when an abbreviation should be swapped for its expansion.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    /// <summary>Tags keystrokes we inject so the hook doesn't treat them as user typing.</summary>
    public static readonly IntPtr InjectedMarker = new(0x54455850);

    private const int ReinstallIntervalMs = 10000;

    private readonly TypedWordTracker _tracker;
    private readonly SynchronizationContext _uiContext;
    private readonly LowLevelKeyboardProc _proc; // held in a field so the GC doesn't collect the callback
    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _translated = new(8);
    private readonly Timer _reinstallTimer;
    private IntPtr _hook;
    private IntPtr _lastWindow;
    private bool _inOwnWindow;
    private bool _enabled = true;

    /// <summary>The field an expansion is waiting at, which gets the keystrokes instead of the tracker.</summary>
    private FieldInput? _field;
    private TaskCompletionSource<bool>? _fieldDone;

    private bool _expandInOwnWindow;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
                EndField(false);
        }
    }

    /// <summary>
    /// Expands in this process's windows too, which is normally off so abbreviations can be typed in the editor.
    /// Changing it forgets the word being typed and stops waiting at a field.
    /// </summary>
    public bool ExpandInOwnWindow
    {
        get => _expandInOwnWindow;
        set
        {
            _expandInOwnWindow = value;
            EndField(false);
        }
    }

    public KeyboardHook(SnippetStore store, AppSettings settings)
    {
        _tracker = new TypedWordTracker(store, settings);
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("KeyboardHook must be created on the UI thread.");
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");

        // Windows silently removes a low-level hook that ever responds too slowly (e.g. while the
        // system is busy), and there's no way to tell when it has, so reinstall it periodically.
        _reinstallTimer = new Timer(_ => _uiContext.Post(_ => Reinstall(), null), null,
            ReinstallIntervalMs, ReinstallIntervalMs);
    }

    /// <remarks>
    /// Runs on the UI thread. Keystrokes are only delivered while that thread waits for messages,
    /// so none are missed or seen twice while both hooks exist.
    /// </remarks>
    private void Reinstall()
    {
        if (_hook == IntPtr.Zero)
            return; // disposed

        var fresh = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (fresh == IntPtr.Zero)
            return;
        UnhookWindowsHookEx(_hook);
        _hook = fresh;
    }

    /// <summary>
    /// Sends the keystrokes the user types to <paramref name="field"/> until they press Tab or Enter
    /// (both swallowed), which finishes it.
    /// </summary>
    /// <returns>
    /// A task that completes with true when the field is finished, or false if it's cancelled by Esc,
    /// another window getting focus, or expansion being turned off.
    /// </returns>
    internal Task<bool> WaitForField(FieldInput field)
    {
        EndField(false);
        _field = field;
        // The caller continues on the UI thread after the hook returns, not inside it.
        _fieldDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _fieldDone.Task;
    }

    private void EndField(bool finished)
    {
        var done = _fieldDone;
        _field = null;
        _fieldDone = null;
        _tracker.Reset();
        done?.SetResult(finished);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (info.dwExtraInfo != InjectedMarker && HandleKeyDown(info))
                return 1; // swallow the key
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <returns>true if the keystroke should be swallowed.</returns>
    private bool HandleKeyDown(KBDLLHOOKSTRUCT info)
    {
        if (!Enabled)
        {
            _tracker.Reset();
            return false;
        }

        var foreground = GetForegroundWindow();
        if (foreground != _lastWindow)
        {
            EndField(false);
            _tracker.Reset();
            _lastWindow = foreground;
            // Don't expand inside our own windows, or you couldn't type abbreviations there.
            _inOwnWindow = IsOwnWindow(foreground);
        }
        if (_inOwnWindow && !ExpandInOwnWindow)
        {
            return false;
        }

        int vk = (int)info.vkCode;
        if (IsModifier(vk))
            return false;

        if (_field is not null)
            return HandleFieldKey(info, foreground, _field);

        if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            _tracker.Reset();
            return false;
        }

        Replacement? replacement;
        switch (vk)
        {
            case VK_BACK:
                _tracker.Backspace();
                return false;
            case VK_SPACE:
            case VK_TAB:
            case VK_RETURN:
                replacement = _tracker.WordEnded((ushort)vk);
                break;
            default:
                string typed = Translate(info, foreground);
                if (typed.Length == 0)
                {
                    // Arrows, Home/End, Delete, F-keys, etc. move the caret, so the word no longer
                    // reflects what's in front of it.
                    _tracker.Reset();
                    return false;
                }
                replacement = _tracker.Typed(typed);
                break;
        }

        if (replacement is null)
            return false;

        // Do the injection after the hook returns; hooks that take too long get removed by Windows.
        _uiContext.Post(_ => TextSender.Replace(replacement, this), null);
        return true;
    }

    /// <returns>true if the keystroke should be swallowed.</returns>
    private bool HandleFieldKey(KBDLLHOOKSTRUCT info, IntPtr foreground, FieldInput field)
    {
        int vk = (int)info.vkCode;
        if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            if (vk == 'V' && IsDown(VK_CONTROL) && !IsDown(VK_MENU))
                field.Type(TextSender.ClipboardText() ?? "");
            return false;
        }

        switch (vk)
        {
            case VK_TAB:
            case VK_RETURN:
                EndField(true);
                return true;
            case VK_ESCAPE:
                EndField(false);
                return true;
            case VK_BACK:
                field.Backspace();
                break;
            case VK_DELETE:
                field.Delete();
                break;
            case VK_LEFT:
                field.Left();
                break;
            case VK_RIGHT:
                field.Right();
                break;
            case VK_HOME:
                field.Home();
                break;
            case VK_END:
                field.End();
                break;
            default:
                string typed = Translate(info, foreground);
                if (typed.Length > 0)
                    field.Type(typed);
                break;
        }
        return false;
    }

    private string Translate(KBDLLHOOKSTRUCT info, IntPtr foreground)
    {
        Array.Clear(_keyState);
        if (IsDown(VK_SHIFT)) _keyState[VK_SHIFT] = 0x80;
        if ((GetKeyState(VK_CAPITAL) & 1) != 0) _keyState[VK_CAPITAL] = 0x01;

        var layout = GetKeyboardLayout(GetWindowThreadProcessId(foreground, out _));
        _translated.Clear();
        // Flag 0x4: don't change the keyboard state (keeps dead keys working in the target app).
        int count = ToUnicodeEx(info.vkCode, info.scanCode, _keyState, _translated, _translated.Capacity, 0x4, layout);
        if (count <= 0)
            return string.Empty;

        string result = _translated.ToString(0, count);
        return result.Any(char.IsControl) ? string.Empty : result;
    }

    private static bool IsOwnWindow(IntPtr window)
    {
        GetWindowThreadProcessId(window, out uint processId);
        return processId == Environment.ProcessId;
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool IsModifier(int vk) =>
        vk is VK_SHIFT or VK_CONTROL or VK_MENU or VK_CAPITAL or VK_LWIN or VK_RWIN
            or >= VK_LSHIFT and <= VK_RMENU;

    public void Dispose()
    {
        EndField(false);
        _reinstallTimer.Dispose();
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
