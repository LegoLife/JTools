#pragma once

#include "Color.h"
#include "imgui.h"

/// Colors for every screen in a dark and a light variant: neutral surfaces with a green accent
/// (the same values as Palette.axaml in the C# app).
/// Layers, back to front: Chrome (side bar, toolbars, status bar) → Background (content) → Card (raised panels).
/// Surface / SurfacePressed are hover and pressed fills on top of any layer.
struct Palette
{
    ImU32 chrome, background, card, surface, surfacePressed, border;
    ImU32 text, textDim;
    ImU32 accent, accentSoft, selection, selectionHover;
    ImU32 inputBackground, inputHover;
    ImU32 warningBackground, warningBorder, warningText;

    /// Filled accent controls (buttons, checkboxes, switches) carry white text, so they use a deeper green than accent.
    ImU32 accentFill, accentFillHover, accentFillPressed;

    Color logBackground, logText;
};

namespace Theme
{
    const Palette& Current();
    bool IsDark();

    /// Rebuilds the ImGui style for the given theme and monitor scale.
    void Apply(bool dark, float dpiScale);

    /// The Windows "app mode" setting (Settings → Personalization → Colors).
    bool WindowsUsesLightTheme();

    float DpiScale();

    /// Loads Segoe UI, Segoe Fluent Icons and Cascadia Mono from the Windows fonts folder.
    void LoadFonts();

    /// ImGui sizes a font by its line height (ascent + descent); Avalonia, like CSS, by its em square.
    /// These convert em sizes (the sizes the C# app used) to ImGui sizes for the UI and monospace fonts.
    float Em(float em);
    float MonoEm(float em);

    // Base (unscaled) font sizes.
    inline float UiFontSize() { return Em(14); }
    inline float CaptionFontSize() { return Em(12); }
}

/// Scales a size in device-independent pixels to the current monitor.
inline float S(float dips) { return dips * Theme::DpiScale(); }
inline ImVec2 S(float x, float y) { return { S(x), S(y) }; }

struct Fonts
{
    ImFont* ui = nullptr;
    ImFont* semibold = nullptr;
    ImFont* mono = nullptr;
};

extern Fonts g_fonts;

/// Segoe Fluent Icons / Segoe MDL2 Assets glyphs.
namespace Icon
{
    inline constexpr const char* Open = "";
    inline constexpr const char* Recent = "";
    inline constexpr const char* ChevronDown = "";
    inline constexpr const char* Highlight = "";
    inline constexpr const char* Appearance = "";
    inline constexpr const char* More = "";
    inline constexpr const char* Document = "";
    inline constexpr const char* Keyboard = "";
    inline constexpr const char* Theme = "";
    inline constexpr const char* Settings = "";
    inline constexpr const char* Warning = "";
    inline constexpr const char* Search = "";
    inline constexpr const char* Add = "";
    inline constexpr const char* Save = "";
    inline constexpr const char* Delete = "";
    inline constexpr const char* Close = "";
}
