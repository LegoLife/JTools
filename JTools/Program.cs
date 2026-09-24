using Avalonia;
using Avalonia.Controls;

namespace JTools;

internal static class Program
{
    /// <summary>Launch argument that starts JTools hidden in the tray (used for "Start with Windows").</summary>
    public const string StartInTrayArgument = "--tray";

    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstance = new Mutex(true, @"Local\JTools.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return; // Already running; it lives in the system tray.
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
