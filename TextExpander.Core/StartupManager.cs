using Microsoft.Win32;

namespace TextExpander;

/// <summary>Toggles launching an app when the user signs in to Windows.</summary>
/// <param name="valueName">Name of the entry under the Run key; unique per app.</param>
/// <param name="arguments">Command-line arguments to launch with, e.g. to start hidden in the tray.</param>
public sealed class StartupManager(string valueName, string arguments = "")
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(valueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(valueName, $"\"{Environment.ProcessPath}\" {arguments}".TrimEnd());
            else
                key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
