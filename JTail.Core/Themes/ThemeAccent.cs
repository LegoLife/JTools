using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace JTail;

/// <summary>
/// Keeps the Fluent accent color matched to the current theme. Fluent reads the accent from the
/// app's top-level resources only (a per-theme value in Palette.axaml falls back to the Windows accent),
/// so it is swapped here whenever the theme changes.
/// </summary>
public static class ThemeAccent
{
    private static readonly Color DarkAccent = Color.Parse("#00FF41");
    private static readonly Color LightAccent = Color.Parse("#0B8A33");

    public static void KeepInSync(Application app)
    {
        Apply(app);
        app.ActualThemeVariantChanged += (_, _) => Apply(app);
    }

    private static void Apply(Application app) =>
        app.Resources["SystemAccentColor"] = app.ActualThemeVariant == ThemeVariant.Light ? LightAccent : DarkAccent;
}
