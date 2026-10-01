using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static TextExpander.NativeMethods;

namespace TextExpander;

/// <summary>Inserts text into whatever window has focus.</summary>
internal static class TextSender
{
    /// <summary>How long to wait for the target app to read the clipboard before restoring it.</summary>
    private const int ClipboardRestoreDelayMs = 600;

    /// <summary>Pause between steps so the target app processes each one before the next arrives.</summary>
    private const int KeyDelayMs = 20;

    /// <summary>
    /// Pause after the keystroke that triggered the expansion. Backspaces injected within ~15 ms of it
    /// are dropped by some apps (e.g. Windows 11 Notepad), leaving the abbreviation in place.
    /// </summary>
    private const int TriggerSettleMs = 50;

    /// <summary>How long Ctrl+V is held down before both keys are released.</summary>
    private const int ChordHoldMs = 50;

    /// <summary>
    /// Deletes the typed abbreviation, pastes the expansion with its placeholders filled in, then
    /// either moves the caret back to <c>{cursor}</c> or presses the trailing key (the trigger key,
    /// when the user wants to keep it).
    /// </summary>
    /// <remarks>
    /// Pasting is used instead of typing the expansion one keystroke at a time because typed
    /// newlines become Enter presses (which send messages in chat apps) and long text gets
    /// dropped or garbled by apps that can't keep up with a burst of injected keystrokes.
    /// </remarks>
    public static async void Replace(Replacement replacement)
    {
        var saved = SnapshotClipboard();
        var (text, caretStepsBack) = SnippetTemplate.Render(replacement.Text, DateTime.Now, TextIn(saved), CultureInfo.CurrentCulture);
        if (!TrySetClipboard(CreateTextData(text.ReplaceLineEndings("\r\n"))))
            return; // Clipboard is locked by another app; leave the abbreviation as typed.
        await Task.Delay(TriggerSettleMs);

        var erase = new List<INPUT>();
        for (int i = 0; i < replacement.Backspaces; i++)
            AddKey(erase, VK_BACK);
        ReleaseHeldModifiers(erase);
        Send(erase);
        await Task.Delay(KeyDelayMs);

        // Hold Ctrl+V down briefly. If the whole chord is sent in one burst, apps that check the
        // live Ctrl state when handling V (e.g. Windows 11 Notepad) see Ctrl already released
        // and type a plain "v" instead of pasting.
        Send([Keyboard(VK_CONTROL, 0), Keyboard('V', 0)]);
        await Task.Delay(ChordHoldMs);
        Send([Keyboard('V', KEYEVENTF_KEYUP), Keyboard(VK_CONTROL, KEYEVENTF_KEYUP)]);

        // With {cursor} the trigger key is dropped: it would land at the end of the text, not at the caret.
        if (caretStepsBack is { } steps)
        {
            await Task.Delay(KeyDelayMs);
            var left = new List<INPUT>();
            for (int i = 0; i < steps; i++)
                AddKey(left, VK_LEFT);
            Send(left);
        }
        else if (replacement.TrailingKey is { } vk)
        {
            await Task.Delay(KeyDelayMs);
            var trailing = new List<INPUT>();
            AddKey(trailing, vk);
            Send(trailing);
        }

        await Task.Delay(ClipboardRestoreDelayMs);
        if (saved is not null)
            TrySetClipboard(saved);
    }

    /// <summary>
    /// If Shift is still physically held (e.g. from typing a capital letter in the abbreviation),
    /// Ctrl+V would become Ctrl+Shift+V, which some apps treat as "paste as plain text" or ignore.
    /// </summary>
    private static void ReleaseHeldModifiers(List<INPUT> inputs)
    {
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
            inputs.Add(Keyboard(VK_SHIFT, KEYEVENTF_KEYUP));
    }

    private static void Send(IReadOnlyCollection<INPUT> inputs)
    {
        var array = inputs.ToArray();
        SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
    }

    private sealed record ClipboardItem(uint Format, byte[] Data);

    /// <summary>Owns the clipboard while we write to it; SetClipboardData can fail when the owner is null.</summary>
    private static IntPtr _clipboardOwner;

    private static uint _excludeFormat;

    private static List<ClipboardItem> CreateTextData(string text)
    {
        var unicodeText = new ClipboardItem(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + "\0"));

        // Keep expansions out of Windows clipboard history (Win+V) and cloud clipboard sync.
        if (_excludeFormat == 0)
            _excludeFormat = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        return [unicodeText, new ClipboardItem(_excludeFormat, new byte[4])];
    }

    private static bool OpenClipboardWithRetry()
    {
        if (_clipboardOwner == IntPtr.Zero)
            _clipboardOwner = CreateWindowEx(0, "STATIC", "JTools clipboard", 0, 0, 0, 0, 0, HWND_MESSAGE,
                IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(_clipboardOwner))
                return true;
            Thread.Sleep(50);
        }
        return false;
    }

    /// <summary>Formats whose handles are GDI objects or private data rather than global memory, so they can't be copied.</summary>
    private static bool IsCopyableFormat(uint format) => format switch
    {
        CF_BITMAP or CF_METAFILEPICT or CF_PALETTE or CF_ENHMETAFILE or CF_OWNERDISPLAY
            or CF_DSPBITMAP or CF_DSPMETAFILEPICT or CF_DSPENHMETAFILE => false,
        >= CF_PRIVATEFIRST and <= CF_PRIVATELAST => false,
        >= CF_GDIOBJFIRST and <= CF_GDIOBJLAST => false,
        _ => true,
    };

    /// <summary>The text in a clipboard snapshot, or null if it has none.</summary>
    private static string? TextIn(List<ClipboardItem>? items)
    {
        var item = items?.FirstOrDefault(i => i.Format == CF_UNICODETEXT);
        if (item is null)
            return null;
        var text = Encoding.Unicode.GetString(item.Data);
        int end = text.IndexOf('\0');
        return end < 0 ? text : text[..end];
    }

    /// <summary>Copies every format currently on the clipboard so it can be put back afterwards.</summary>
    private static List<ClipboardItem>? SnapshotClipboard()
    {
        if (!OpenClipboardWithRetry())
            return null;

        var items = new List<ClipboardItem>();
        try
        {
            for (uint format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
            {
                if (!IsCopyableFormat(format))
                    continue;
                // Some formats (e.g. delay-rendered ones) can't be read back; skip them.
                var handle = GetClipboardData(format);
                var size = handle == IntPtr.Zero ? 0 : (int)GlobalSize(handle);
                var data = size == 0 ? IntPtr.Zero : GlobalLock(handle);
                if (data == IntPtr.Zero)
                    continue;
                var bytes = new byte[size];
                Marshal.Copy(data, bytes, 0, size);
                GlobalUnlock(handle);
                items.Add(new ClipboardItem(format, bytes));
            }
        }
        finally
        {
            CloseClipboard();
        }
        return items.Count > 0 ? items : null;
    }

    private static bool TrySetClipboard(List<ClipboardItem> items)
    {
        if (!OpenClipboardWithRetry())
            return false;

        bool ok = true;
        try
        {
            EmptyClipboard();
            foreach (var item in items)
            {
                var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)item.Data.Length);
                var target = memory == IntPtr.Zero ? IntPtr.Zero : GlobalLock(memory);
                if (target == IntPtr.Zero)
                {
                    if (memory != IntPtr.Zero)
                        GlobalFree(memory);
                    ok = false;
                    continue;
                }
                Marshal.Copy(item.Data, 0, target, item.Data.Length);
                GlobalUnlock(memory);
                if (SetClipboardData(item.Format, memory) == IntPtr.Zero)
                {
                    GlobalFree(memory);
                    ok = false;
                }
            }
        }
        finally
        {
            CloseClipboard();
        }
        return ok;
    }

    private static void AddKey(List<INPUT> inputs, int vk)
    {
        inputs.Add(Keyboard(vk, 0));
        inputs.Add(Keyboard(vk, KEYEVENTF_KEYUP));
    }

    /// <remarks>
    /// Includes the hardware scan code, which some apps read instead of the virtual-key code.
    /// </remarks>
    private static INPUT Keyboard(int vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = (ushort)MapVirtualKey((uint)vk, MAPVK_VK_TO_VSC),
                dwFlags = flags,
                dwExtraInfo = KeyboardHook.InjectedMarker,
            },
        },
    };
}
