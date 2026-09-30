#include "Widgets.h"

#include "Theme.h"
#include "imgui_internal.h"

#include <cfloat>
#include <vector>

namespace
{
    const Palette& P()
    {
        return Theme::Current();
    }

    float TextY(ImVec2 min, float height)
    {
        return min.y + (height - ImGui::GetFontSize()) * 0.5f;
    }

    std::vector<size_t> CodepointBoundaries(const std::string& text)
    {
        std::vector<size_t> boundaries;
        for (size_t i = 0; i < text.size(); i++)
            if (((unsigned char)text[i] & 0xC0) != 0x80)
                boundaries.push_back(i);
        boundaries.push_back(text.size());
        return boundaries;
    }
}

ImU32 Widgets::Col(ImU32 color)
{
    return ImGui::GetColorU32(color);
}

bool Widgets::ToolButton(const char* id, const char* icon, const char* label, const char* trailingIcon)
{
    const float height = S(32), padding = S(10), gap = S(6);
    float iconWidth = ImGui::CalcTextSize(icon).x;
    float labelWidth = label ? ImGui::CalcTextSize(label).x : 0;
    float trailingSize = S(Theme::Em(9));
    float trailingWidth = trailingIcon
        ? ImGui::GetFont()->CalcTextSizeA(trailingSize, FLT_MAX, 0, trailingIcon).x : 0;

    float width = padding * 2 + iconWidth + (label ? gap + labelWidth : 0) + (trailingIcon ? gap + trailingWidth : 0);
    ImVec2 min = ImGui::GetCursorScreenPos();
    bool clicked = ImGui::InvisibleButton(id, ImVec2(width, height));
    bool hovered = ImGui::IsItemHovered(), held = ImGui::IsItemActive();

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    if (hovered || held)
        drawList->AddRectFilled(min, ImVec2(min.x + width, min.y + height), Col(held ? P().surfacePressed : P().surface), S(4));

    ImU32 textColor = Col(P().text);
    float x = min.x + padding, y = TextY(min, height);
    drawList->AddText(ImVec2(x, y), textColor, icon);
    x += iconWidth + gap;
    if (label)
    {
        drawList->AddText(ImVec2(x, y), textColor, label);
        x += labelWidth + gap;
    }
    if (trailingIcon)
        drawList->AddText(ImGui::GetFont(), trailingSize, ImVec2(x, min.y + (height - trailingSize) * 0.5f),
                          Col(P().textDim), trailingIcon);
    return clicked;
}

bool Widgets::NavButton(const char* id, const char* icon, const char* label, bool selected, float width)
{
    const float height = S(NavButtonHeight), iconSize = S(Theme::Em(21)), labelSize = S(Theme::Em(12.5f));
    ImVec2 min = ImGui::GetCursorScreenPos();
    ImVec2 max(min.x + width, min.y + height);
    bool clicked = ImGui::InvisibleButton(id, ImVec2(width, height));
    bool hovered = ImGui::IsItemHovered(), held = ImGui::IsItemActive();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    if (held)
        drawList->AddRectFilled(min, max, Col(P().surfacePressed), S(4));
    else if (selected || hovered)
        drawList->AddRectFilled(min, max, Col(P().surface), S(4));

    if (selected)
    {
        float pillHeight = S(18), centerY = (min.y + max.y) * 0.5f;
        drawList->AddRectFilled(ImVec2(min.x, centerY - pillHeight / 2), ImVec2(min.x + S(3), centerY + pillHeight / 2),
                                Col(P().accent), S(2));
    }

    ImFont* font = ImGui::GetFont();
    ImVec2 iconExtent = font->CalcTextSizeA(iconSize, FLT_MAX, 0, icon);
    ImVec2 labelExtent = font->CalcTextSizeA(labelSize, FLT_MAX, 0, label);
    float top = min.y + (height - iconExtent.y - S(4) - labelExtent.y) * 0.5f;
    float centerX = (min.x + max.x) * 0.5f;

    ImU32 textColor = Col(selected || hovered ? P().text : P().textDim);
    drawList->AddText(font, iconSize, ImVec2(centerX - iconExtent.x / 2, top), Col(selected ? P().accent : textColor), icon);
    drawList->AddText(font, labelSize, ImVec2(centerX - labelExtent.x / 2, top + iconExtent.y + S(4)), textColor, label);
    return clicked;
}

float Widgets::ButtonWidth(const char* label, const char* icon)
{
    float width = S(14) * 2 + ImGui::CalcTextSize(label).x;
    if (icon)
        width += ImGui::CalcTextSize(icon).x + S(8);
    return width;
}

bool Widgets::Button(const char* label, const char* icon, bool accent, float width)
{
    const float height = S(32);
    if (width <= 0)
        width = ButtonWidth(label, icon);

    ImVec2 min = ImGui::GetCursorScreenPos();
    ImVec2 max(min.x + width, min.y + height);
    bool clicked = ImGui::InvisibleButton(label, ImVec2(width, height));
    bool hovered = ImGui::IsItemHovered(), held = ImGui::IsItemActive();

    const Palette& p = P();
    ImU32 fill = accent ? (held ? p.accentFillPressed : hovered ? p.accentFillHover : p.accentFill)
                        : (held ? p.border : hovered ? p.surfacePressed : p.surface);
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(min, max, Col(fill), S(4));

    const char* labelEnd = ImGui::FindRenderedTextEnd(label);
    float contentWidth = ImGui::CalcTextSize(label, labelEnd).x;
    float iconWidth = icon ? ImGui::CalcTextSize(icon).x : 0;
    if (icon)
        contentWidth += iconWidth + S(8);

    ImU32 textColor = Col(accent ? IM_COL32_WHITE : p.text);
    float x = min.x + (width - contentWidth) * 0.5f, y = TextY(min, height);
    if (icon)
    {
        drawList->AddText(ImVec2(x, y), textColor, icon);
        x += iconWidth + S(8);
    }
    drawList->AddText(ImVec2(x, y), textColor, label, labelEnd);
    return clicked;
}

bool Widgets::Checkbox(const char* label, bool* value)
{
    const float box = S(18), rowHeight = S(24);
    const char* labelEnd = ImGui::FindRenderedTextEnd(label);
    float labelWidth = ImGui::CalcTextSize(label, labelEnd).x;

    ImVec2 min = ImGui::GetCursorScreenPos();
    bool clicked = ImGui::InvisibleButton(label, ImVec2(box + S(8) + labelWidth, rowHeight));
    if (clicked)
        *value = !*value;
    bool hovered = ImGui::IsItemHovered();

    const Palette& p = P();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    ImVec2 boxMin(min.x, min.y + (rowHeight - box) * 0.5f), boxMax(boxMin.x + box, boxMin.y + box);
    if (*value)
    {
        drawList->AddRectFilled(boxMin, boxMax, Col(hovered ? p.accentFillHover : p.accentFill), S(4));
        float pad = box * 0.2f;
        ImGui::RenderCheckMark(drawList, ImVec2(boxMin.x + pad, boxMin.y + pad), Col(IM_COL32_WHITE), box - pad * 2);
    }
    else
    {
        drawList->AddRectFilled(boxMin, boxMax, Col(p.inputBackground), S(4));
        drawList->AddRect(boxMin, boxMax, Col(hovered ? p.text : p.textDim), S(4), S(1));
    }
    drawList->AddText(ImVec2(boxMax.x + S(8), TextY(min, rowHeight)), Col(p.text), label, labelEnd);
    return clicked;
}

bool Widgets::RadioButton(const char* label, bool active)
{
    const float radius = S(9), rowHeight = S(24);
    const char* labelEnd = ImGui::FindRenderedTextEnd(label);
    float labelWidth = ImGui::CalcTextSize(label, labelEnd).x;

    ImVec2 min = ImGui::GetCursorScreenPos();
    bool clicked = ImGui::InvisibleButton(label, ImVec2(radius * 2 + S(8) + labelWidth, rowHeight));
    bool hovered = ImGui::IsItemHovered();

    const Palette& p = P();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    ImVec2 center(min.x + radius, min.y + rowHeight * 0.5f);
    if (active)
    {
        drawList->AddCircleFilled(center, radius, Col(hovered ? p.accentFillHover : p.accentFill));
        drawList->AddCircleFilled(center, radius * 0.42f, Col(IM_COL32_WHITE));
    }
    else
    {
        drawList->AddCircleFilled(center, radius, Col(p.inputBackground));
        drawList->AddCircle(center, radius, Col(hovered ? p.text : p.textDim), 0, S(1));
    }
    drawList->AddText(ImVec2(min.x + radius * 2 + S(8), TextY(min, rowHeight)), Col(p.text), label, labelEnd);
    return clicked;
}

bool Widgets::ToggleSwitch(const char* id, bool* value, const char* label)
{
    const float trackWidth = S(40), trackHeight = S(20), rowHeight = S(28);
    float labelWidth = ImGui::CalcTextSize(label).x;

    ImVec2 min = ImGui::GetCursorScreenPos();
    bool clicked = ImGui::InvisibleButton(id, ImVec2(trackWidth + S(10) + labelWidth, rowHeight));
    if (clicked)
        *value = !*value;
    bool hovered = ImGui::IsItemHovered();

    const Palette& p = P();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    ImVec2 trackMin(min.x, min.y + (rowHeight - trackHeight) * 0.5f);
    ImVec2 trackMax(trackMin.x + trackWidth, trackMin.y + trackHeight);
    float knobRadius = S(6), centerY = (trackMin.y + trackMax.y) * 0.5f;
    if (*value)
    {
        drawList->AddRectFilled(trackMin, trackMax, Col(hovered ? p.accentFillHover : p.accentFill), trackHeight);
        drawList->AddCircleFilled(ImVec2(trackMax.x - trackHeight * 0.5f, centerY), knobRadius, Col(IM_COL32_WHITE));
    }
    else
    {
        drawList->AddRectFilled(trackMin, trackMax, Col(p.inputBackground), trackHeight);
        drawList->AddRect(trackMin, trackMax, Col(hovered ? p.text : p.textDim), trackHeight, S(1));
        drawList->AddCircleFilled(ImVec2(trackMin.x + trackHeight * 0.5f, centerY), knobRadius * 0.85f,
                                  Col(hovered ? p.text : p.textDim));
    }
    drawList->AddText(ImVec2(trackMax.x + S(10), TextY(min, rowHeight)), Col(p.text), label);
    return clicked;
}

void Widgets::WarningBanner(const char* text, float width, bool withIcon)
{
    const Palette& p = P();
    const float padX = S(12), padY = S(10);
    float iconWidth = withIcon ? ImGui::CalcTextSize(Icon::Warning).x + S(10) : 0;
    float wrapWidth = width - padX * 2 - iconWidth;

    ImFont* font = ImGui::GetFont();
    float fontSize = ImGui::GetFontSize();
    ImVec2 textSize = font->CalcTextSizeA(fontSize, FLT_MAX, wrapWidth, text);

    ImVec2 min = ImGui::GetCursorScreenPos();
    ImVec2 max(min.x + width, min.y + textSize.y + padY * 2);
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(min, max, Col(p.warningBackground), S(6));
    drawList->AddRect(min, max, Col(p.warningBorder), S(6), S(1));
    if (withIcon)
        drawList->AddText(ImVec2(min.x + padX, min.y + padY), Col(p.warningText), Icon::Warning);
    drawList->AddText(font, fontSize, ImVec2(min.x + padX + iconWidth, min.y + padY), Col(p.warningText), text, nullptr,
                      wrapWidth);
    ImGui::Dummy(ImVec2(width, max.y - min.y));
}

void Widgets::TextColored(ImU32 color, const char* text)
{
    ImGui::PushStyleColor(ImGuiCol_Text, color);
    ImGui::TextUnformatted(text);
    ImGui::PopStyleColor();
}

void Widgets::TextDim(const char* text)
{
    TextColored(P().textDim, text);
}

void Widgets::Caption(const char* text, float wrapWidth)
{
    ImGui::PushFont(nullptr, Theme::CaptionFontSize());
    ImGui::PushStyleColor(ImGuiCol_Text, P().textDim);
    if (wrapWidth != 0)
    {
        ImGui::PushTextWrapPos(wrapWidth < 0 ? 0.0f : ImGui::GetCursorPosX() + wrapWidth);
        ImGui::TextUnformatted(text);
        ImGui::PopTextWrapPos();
    }
    else
        ImGui::TextUnformatted(text);
    ImGui::PopStyleColor();
    ImGui::PopFont();
}

void Widgets::SectionHeader(const char* text, float size)
{
    ImGui::PushFont(g_fonts.semibold, Theme::Em(size));
    ImGui::TextUnformatted(text);
    ImGui::PopFont();
}

void Widgets::FieldLabel(const char* text)
{
    ImGui::PushFont(nullptr, Theme::Em(13));
    TextDim(text);
    ImGui::PopFont();
    ImGui::SetCursorPosY(ImGui::GetCursorPosY() - ImGui::GetStyle().ItemSpacing.y + S(4));
}

void Widgets::DashedRoundedRect(ImDrawList* drawList, ImVec2 min, ImVec2 max, ImU32 color, float rounding,
                                float thickness, float dash, float gap)
{
    drawList->PathRect(min, max, rounding);
    std::vector<ImVec2> points(drawList->_Path.begin(), drawList->_Path.end());
    drawList->PathClear();
    if (points.size() < 2)
        return;
    points.push_back(points[0]);

    // Walk the outline, drawing "dash" length on and "gap" length off.
    bool drawing = true;
    float remaining = dash;
    for (size_t i = 0; i + 1 < points.size(); i++)
    {
        ImVec2 a = points[i], b = points[i + 1];
        float length = ImSqrt(ImLengthSqr(ImVec2(b.x - a.x, b.y - a.y)));
        float at = 0;
        while (at < length)
        {
            float step = ImMin(remaining, length - at);
            if (drawing)
            {
                ImVec2 from(a.x + (b.x - a.x) * (at / length), a.y + (b.y - a.y) * (at / length));
                ImVec2 to(a.x + (b.x - a.x) * ((at + step) / length), a.y + (b.y - a.y) * ((at + step) / length));
                drawList->AddLine(from, to, color, thickness);
            }
            at += step;
            remaining -= step;
            if (remaining <= 0)
            {
                drawing = !drawing;
                remaining = drawing ? dash : gap;
            }
        }
    }
}

std::string Widgets::EllipsizeEnd(const std::string& text, float maxWidth)
{
    if (ImGui::CalcTextSize(text.c_str()).x <= maxWidth)
        return text;

    auto boundaries = CodepointBoundaries(text);
    size_t low = 0, high = boundaries.size() - 1;
    while (low < high)
    {
        size_t mid = (low + high + 1) / 2;
        std::string candidate = text.substr(0, boundaries[mid]) + "…";
        if (ImGui::CalcTextSize(candidate.c_str()).x <= maxWidth)
            low = mid;
        else
            high = mid - 1;
    }
    return text.substr(0, boundaries[low]) + "…";
}

std::string Widgets::EllipsizeStart(const std::string& text, float maxWidth)
{
    if (ImGui::CalcTextSize(text.c_str()).x <= maxWidth)
        return text;

    auto boundaries = CodepointBoundaries(text);
    size_t low = 0, high = boundaries.size() - 1;
    while (low < high)
    {
        size_t mid = (low + high) / 2;
        std::string candidate = "…" + text.substr(boundaries[mid]);
        if (ImGui::CalcTextSize(candidate.c_str()).x <= maxWidth)
            high = mid;
        else
            low = mid + 1;
    }
    return "…" + text.substr(boundaries[low]);
}

bool Widgets::BeginDialog(const char* title, float width)
{
    ImGui::SetNextWindowPos(ImGui::GetMainViewport()->GetCenter(), ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));
    ImGui::SetNextWindowSizeConstraints(ImVec2(width, 0), ImVec2(width, FLT_MAX));
    return ImGui::BeginPopupModal(title, nullptr, ImGuiWindowFlags_AlwaysAutoResize | ImGuiWindowFlags_NoSavedSettings);
}

void Widgets::DialogFooter()
{
    ImVec2 windowPos = ImGui::GetWindowPos(), windowSize = ImGui::GetWindowSize();
    ImVec2 windowMax(windowPos.x + windowSize.x, windowPos.y + windowSize.y);
    float top = ImGui::GetCursorScreenPos().y + S(4);
    float border = ImGui::GetStyle().WindowBorderSize;

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->PushClipRect(windowPos, windowMax, false);
    drawList->AddRectFilled(ImVec2(windowPos.x + border, top), ImVec2(windowMax.x - border, windowMax.y - border),
                            Col(P().chrome), ImGui::GetStyle().WindowRounding, ImDrawFlags_RoundCornersBottom);
    drawList->AddLine(ImVec2(windowPos.x + border, top), ImVec2(windowMax.x - border, top), Col(P().border), S(1));
    drawList->PopClipRect();

    ImGui::SetCursorScreenPos(ImVec2(ImGui::GetCursorScreenPos().x, top + ImGui::GetStyle().WindowPadding.y));
}

void Widgets::AlignRight(float width)
{
    ImGui::SetCursorPosX(ImGui::GetCursorPosX() + ImGui::GetContentRegionAvail().x - width);
}

bool Widgets::DialogCancelPressed()
{
    return ImGui::IsWindowFocused() && !ImGui::IsAnyItemActive() && ImGui::IsKeyPressed(ImGuiKey_Escape, false);
}
