#pragma once

#include "imgui.h"

#include <string>

/// Controls drawn to match the C# app's Fluent look (toolbars, cards, switches, banners).
namespace Widgets
{
    /// Applies the current style alpha (so custom-drawn controls dim inside BeginDisabled).
    ImU32 Col(ImU32 color);

    /// A flat toolbar button: icon, label and an optional trailing icon (e.g. a dropdown chevron).
    bool ToolButton(const char* id, const char* icon, const char* label, const char* trailingIcon = nullptr);

    /// Unscaled height of a NavButton.
    constexpr float NavButtonHeight = 70;

    /// A side bar button: icon over a small label, with a filled background and accent pill when selected.
    bool NavButton(const char* id, const char* icon, const char* label, bool selected, float width);

    /// A regular or accent (filled green) button with an optional leading icon. A zero width sizes to the content.
    bool Button(const char* label, const char* icon = nullptr, bool accent = false, float width = 0);
    float ButtonWidth(const char* label, const char* icon = nullptr);

    bool Checkbox(const char* label, bool* value);
    bool RadioButton(const char* label, bool active);

    /// An on/off switch followed by a label.
    bool ToggleSwitch(const char* id, bool* value, const char* label);

    /// A warning banner with wrapped text.
    void WarningBanner(const char* text, float width, bool withIcon = false);

    void TextColored(ImU32 color, const char* text);
    void TextDim(const char* text);
    void Caption(const char* text, float wrapWidth = -1);
    void SectionHeader(const char* text, float size = 14.0f);
    void FieldLabel(const char* text);

    void DashedRoundedRect(ImDrawList* drawList, ImVec2 min, ImVec2 max, ImU32 color, float rounding, float thickness,
                           float dash, float gap);

    /// Shortens text to fit a width, ending with "…" (or starting with it, for paths).
    std::string EllipsizeEnd(const std::string& text, float maxWidth);
    std::string EllipsizeStart(const std::string& text, float maxWidth);

    /// Opens a centered, fixed-width modal dialog (sized to its content vertically).
    bool BeginDialog(const char* title, float width);

    /// Draws the chrome-colored footer band behind the dialog's buttons and moves the cursor into it.
    /// Call after the dialog's content; the buttons go right after.
    void DialogFooter();

    /// Moves the cursor so the next items (of the given total width) end at the right edge.
    void AlignRight(float width);

    /// True when Escape was pressed in the current dialog and nothing inside it (an input, a color popup) used it.
    bool DialogCancelPressed();
}
