using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;

namespace JTools;

/// <summary>
/// Ctrl+Alt+J, pressed in any app. Windows delivers it to a window's message loop, so it keeps working
/// while that window is hidden in the tray.
/// </summary>
internal sealed class GlobalHotkey : IDisposable
{
    public static readonly KeyGesture Gesture = new(Key.J, KeyModifiers.Control | KeyModifiers.Alt);

    private const uint WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000; // holding the keys down fires once, not repeatedly
    private const uint VK_J = 0x4A;
    private const int HotkeyId = 1;

    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly Action _pressed;
    private readonly Win32Properties.CustomWndProcHookCallback _wndProc; // held in a field so the GC doesn't collect it

    private GlobalHotkey(Window window, IntPtr hwnd, Action pressed)
    {
        _window = window;
        _hwnd = hwnd;
        _pressed = pressed;
        _wndProc = WndProc;
        Win32Properties.AddWndProcHookCallback(window, _wndProc);
    }

    /// <summary>Registers the hotkey with <paramref name="window"/>, which needn't be shown.</summary>
    /// <returns>The hotkey, or null if another app already uses the combination.</returns>
    public static GlobalHotkey? TryRegister(Window window, Action pressed)
    {
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd
            || !RegisterHotKey(hwnd, HotkeyId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_J))
        {
            return null;
        }

        return new GlobalHotkey(window, hwnd, pressed);
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam == HotkeyId)
        {
            handled = true;
            _pressed();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_hwnd, HotkeyId);
        Win32Properties.RemoveWndProcHookCallback(_window, _wndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
