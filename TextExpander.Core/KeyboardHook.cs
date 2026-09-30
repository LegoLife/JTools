using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using static TextExpander.NativeMethods;

namespace TextExpander;

/// <summary>
/// Watches keystrokes system-wide, tracks the word currently being typed, and swaps an
/// abbreviation for its expansion either as soon as it's typed or when a trigger key is pressed,
/// depending on <see cref="AppSettings.Mode"/>.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    /// <summary>Tags keystrokes we inject so the hook doesn't treat them as user typing.</summary>
    public static readonly IntPtr InjectedMarker = new(0x54455850);

    private const int MaxBufferLength = 64;
    private const int ReinstallIntervalMs = 10000;

    private readonly SnippetStore _store;
    private readonly AppSettings _settings;
    private readonly SynchronizationContext _uiContext;
    private readonly LowLevelKeyboardProc _proc; // held in a field so the GC doesn't collect the callback
    private readonly StringBuilder _buffer = new();
    private readonly byte[] _keyState = new byte[256];
    private readonly StringBuilder _translated = new(8);
    private readonly Timer _reinstallTimer;
    private IntPtr _hook;
    private IntPtr _lastWindow;

    public bool Enabled { get; set; } = true;

    public KeyboardHook(SnippetStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;
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
            _buffer.Clear();
            return false;
        }

        var foreground = GetForegroundWindow();
        if (foreground != _lastWindow)
        {
            _buffer.Clear();
            _lastWindow = foreground;
        }

        int vk = (int)info.vkCode;
        if (IsModifier(vk))
            return false;

        if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
        {
            _buffer.Clear();
            return false;
        }

        switch (vk)
        {
            case VK_BACK:
                if (_buffer.Length > 0) _buffer.Length--;
                return false;
            case VK_SPACE:
            case VK_TAB:
            case VK_RETURN:
                if (_settings.Mode == ExpandMode.TriggerKey && _settings.IsTriggerKey(vk))
                    return TryExpandOnTrigger((ushort)vk, foreground);
                _buffer.Clear(); // whitespace ends the word
                return false;
        }

        string typed = Translate(info, foreground);
        if (typed.Length == 0)
        {
            // Arrows, Home/End, Delete, F-keys, etc. move the caret, so the buffer no longer
            // reflects what's in front of it.
            _buffer.Clear();
            return false;
        }

        _buffer.Append(typed);
        if (_buffer.Length > MaxBufferLength)
            _buffer.Remove(0, _buffer.Length - MaxBufferLength);

        if (_settings.Mode == ExpandMode.Immediate)
            return TryExpandImmediately(typed.Length, foreground);
        return false;
    }

    private bool TryExpandOnTrigger(ushort triggerVk, IntPtr foreground)
    {
        var snippet = FindMatch(foreground);
        _buffer.Clear();
        if (snippet is null)
            return false;

        // The trigger key is swallowed, so everything typed so far is on screen.
        ushort? trailingKey = _settings.KeepTriggerKey ? triggerVk : null;
        PostReplace(snippet.Abbreviation.Length, snippet.Expansion, trailingKey);
        return true;
    }

    private bool TryExpandImmediately(int lastKeyLength, IntPtr foreground)
    {
        var snippet = FindMatch(foreground);
        if (snippet is null)
            return false;

        _buffer.Clear();
        // The keystroke that completed the abbreviation is swallowed, so it never reaches the screen.
        int onScreen = Math.Max(0, snippet.Abbreviation.Length - lastKeyLength);
        PostReplace(onScreen, snippet.Expansion, null);
        return true;
    }

    private Snippet? FindMatch(IntPtr foreground)
    {
        // Don't expand inside our own windows, or you couldn't type abbreviations there.
        if (_buffer.Length == 0 || IsOwnWindow(foreground))
            return null;
        return _store.FindMatch(_buffer.ToString());
    }

    private void PostReplace(int backspaces, string expansion, ushort? trailingKey)
    {
        // Do the injection after the hook returns; hooks that take too long get removed by Windows.
        _uiContext.Post(_ => TextSender.Replace(backspaces, expansion, trailingKey), null);
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
        _reinstallTimer.Dispose();
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
