using System.Runtime.InteropServices;
using static TextExpander.NativeMethods;

namespace TextExpander;

/// <summary>Inserts text into whatever window has focus.</summary>
internal static class TextSender
{
    /// <summary>How long to wait for the target app to read the clipboard before restoring it.</summary>
    private const int ClipboardRestoreDelayMs = 600;

    /// <summary>Pause between steps so the target app processes each one before the next arrives.</summary>
    private const int KeyDelayMs = 20;

    /// <summary>How long Ctrl+V is held down before both keys are released.</summary>
    private const int ChordHoldMs = 50;

    /// <summary>
    /// Deletes the typed abbreviation, pastes the expansion, then optionally presses
    /// <paramref name="trailingVk"/> (the trigger key, when the user wants to keep it).
    /// </summary>
    /// <remarks>
    /// Pasting is used instead of typing the expansion one keystroke at a time because typed
    /// newlines become Enter presses (which send messages in chat apps) and long text gets
    /// dropped or garbled by apps that can't keep up with a burst of injected keystrokes.
    /// </remarks>
    public static async void Replace(int backspaces, string expansion, ushort? trailingVk)
    {
        var saved = SnapshotClipboard();
        if (!TrySetClipboard(CreateTextData(expansion.ReplaceLineEndings("\r\n"))))
            return; // Clipboard is locked by another app; leave the abbreviation as typed.

        var erase = new List<INPUT>();
        for (int i = 0; i < backspaces; i++)
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

        if (trailingVk is { } vk)
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

    private static DataObject CreateTextData(string text)
    {
        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        // Keep expansions out of Windows clipboard history (Win+V) and cloud clipboard sync.
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        return data;
    }

    /// <summary>Copies every format currently on the clipboard so it can be put back afterwards.</summary>
    private static DataObject? SnapshotClipboard()
    {
        try
        {
            var current = Clipboard.GetDataObject();
            if (current is null)
                return null;

            var copy = new DataObject();
            foreach (var format in current.GetFormats(autoConvert: false))
            {
                try
                {
                    if (current.GetData(format, autoConvert: false) is { } value)
                        copy.SetData(format, value);
                }
                catch (Exception e) when (e is ExternalException or COMException or OutOfMemoryException)
                {
                    // Some formats (e.g. delay-rendered ones) can't be read back; skip them.
                }
            }
            return copy.GetFormats().Length > 0 ? copy : null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static bool TrySetClipboard(DataObject data)
    {
        try
        {
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 50);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
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
