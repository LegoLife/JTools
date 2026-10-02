using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;

namespace JTools;

internal static class Program
{
    /// <summary>Launch argument that starts JTools hidden in the tray (used for "Start with Windows").</summary>
    public const string StartInTrayArgument = "--tray";

    /// <summary>Set before shutting down to start a new JTools once this one has exited (e.g. after an import).</summary>
    public static bool RestartAfterExit { get; set; }

    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstance = new Mutex(true, @"Local\JTools.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return; // Already running; it lives in the system tray.
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);

        if (RestartAfterExit && Environment.ProcessPath is { } exePath)
        {
            // Let go of the single-instance mutex first, or the new JTools would see this one and quit.
            singleInstance.ReleaseMutex();
            Process.Start(exePath);
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
