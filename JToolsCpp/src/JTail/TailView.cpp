#include "TailView.h"

#include "../Common/Theme.h"
#include "../Common/Util.h"
#include "../Common/Widgets.h"
#include "../JTools/Platform.h"
#include "imgui_internal.h"
#include "imgui_stdlib.h"

#include <cfloat>
#include <cmath>

using namespace Widgets;

namespace
{
    const char* HighlightsDialogId = "Line Highlights";
    const char* AppearanceDialogId = "Log Appearance";
    const char* MessageDialogId = "###message";

    constexpr ImGuiColorEditFlags SwatchFlags =
        ImGuiColorEditFlags_NoInputs | ImGuiColorEditFlags_AlphaBar | ImGuiColorEditFlags_AlphaPreviewHalf;

    const Palette& P()
    {
        return Theme::Current();
    }

    double RelativeLuminance(Color color)
    {
        return (0.299 * color.r + 0.587 * color.g + 0.114 * color.b) / 255.0;
    }

    /// A color swatch that opens a picker; returns true while the color changes.
    bool ColorSwatch(const char* id, Color& color)
    {
        ImVec4 value = color.ToVec4();
        if (!ImGui::ColorEdit4(id, &value.x, SwatchFlags))
            return false;
        color = Color::FromVec4(value);
        return true;
    }

    /// A vertically centered label in a settings row, with the control placed at a fixed column.
    void RowLabel(const char* label, float column)
    {
        ImGui::AlignTextToFramePadding();
        ImGui::TextUnformatted(label);
        ImGui::SameLine(ImGui::GetStyle().WindowPadding.x + column);
    }
}

TailView::TailView() = default;

Color TailView::LogBackground() const
{
    return logTheme_.background.value_or(P().logBackground);
}

Color TailView::LogText() const
{
    return logTheme_.text.value_or(P().logText);
}

TailView::Tab* TailView::SelectedTab()
{
    for (Tab& tab : tabs_)
        if (tab.id == selectedTabId_)
            return &tab;
    return nullptr;
}

std::string TailView::Title() const
{
    for (const Tab& tab : tabs_)
        if (tab.id == selectedTabId_)
            return std::string(DefaultTitle) + " - " + FileNameOf(tab.path);
    return DefaultTitle;
}

void TailView::Draw(ImVec2 size)
{
    const float toolbarHeight = S(46), statusBarHeight = S(32);
    ImVec2 origin = ImGui::GetCursorScreenPos();

    DrawToolbar(size.x);

    ImGui::SetCursorScreenPos(ImVec2(origin.x, origin.y + toolbarHeight));
    ImVec2 middle(size.x, size.y - toolbarHeight - statusBarHeight);
    if (tabs_.empty())
        DrawEmptyState(middle);
    else
        DrawTabs(middle);

    ImGui::SetCursorScreenPos(ImVec2(origin.x, origin.y + size.y - statusBarHeight));
    DrawStatusBar(size.x);

    if (!ImGui::IsPopupOpen(nullptr, ImGuiPopupFlags_AnyPopupId | ImGuiPopupFlags_AnyPopupLevel)
        && ImGui::Shortcut(ImGuiMod_Ctrl | ImGuiKey_O, ImGuiInputFlags_RouteGlobal))
        ShowOpenDialog();

    if (openHighlights_)
        ImGui::OpenPopup(HighlightsDialogId);
    if (openAppearance_)
        ImGui::OpenPopup(AppearanceDialogId);
    if (openMessage_)
        ImGui::OpenPopup(MessageDialogId);
    openHighlights_ = openAppearance_ = openMessage_ = false;

    DrawHighlightsDialog();
    DrawAppearanceDialog();
    DrawMessageDialog();
}

void TailView::DrawToolbar(float width)
{
    const float height = S(46);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + width, origin.y + height), P().chrome);
    drawList->AddLine(ImVec2(origin.x, origin.y + height - 1), ImVec2(origin.x + width, origin.y + height - 1), P().border);

    float top = origin.y + (height - S(32)) * 0.5f;
    ImGui::SetCursorScreenPos(ImVec2(origin.x + S(8), top));
    if (ToolButton("##open", Icon::Open, "Open"))
        ShowOpenDialog();
    ImGui::SetItemTooltip("Open a log file (Ctrl+O)");

    ImGui::SameLine(0, S(2));
    if (ToolButton("##recent", Icon::Recent, "Recent", Icon::ChevronDown))
        ImGui::OpenPopup("##recentMenu");
    ImGui::SetItemTooltip("Recently opened files");
    ImVec2 recentAnchor(ImGui::GetItemRectMin().x, ImGui::GetItemRectMax().y + S(4));

    // Separator between the file buttons and the view buttons.
    ImGui::SameLine(0, S(6));
    ImVec2 separator = ImGui::GetCursorScreenPos();
    drawList->AddLine(ImVec2(separator.x, separator.y + S(7)), ImVec2(separator.x, separator.y + S(25)), P().border, S(1));
    ImGui::Dummy(ImVec2(S(1), S(32)));

    ImGui::SameLine(0, S(6));
    if (ToolButton("##highlights", Icon::Highlight, "Highlights"))
        openHighlights_ = true;
    ImGui::SetItemTooltip("Highlight lines containing chosen words");

    ImGui::SameLine(0, S(2));
    if (ToolButton("##appearance", Icon::Appearance, "Appearance"))
        openAppearance_ = true;
    ImGui::SetItemTooltip("Log colors and font size");

    float moreWidth = S(20) + ImGui::CalcTextSize(Icon::More).x;
    ImGui::SetCursorScreenPos(ImVec2(origin.x + width - S(8) - moreWidth, top));
    if (ToolButton("##more", Icon::More, nullptr))
        ImGui::OpenPopup("##moreMenu");
    ImGui::SetItemTooltip("More");
    ImVec2 moreAnchor(ImGui::GetItemRectMax().x, ImGui::GetItemRectMax().y + S(4));

    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(6, 6));
    ImGui::SetNextWindowPos(recentAnchor);
    if (ImGui::BeginPopup("##recentMenu"))
    {
        if (recentFiles_.paths.empty())
            ImGui::MenuItem("(no recent files)", nullptr, false, false);
        std::string clicked;
        for (const std::string& path : recentFiles_.paths)
        {
            ImGui::PushID(path.c_str());
            if (ImGui::MenuItem(EllipsizeStart(path, S(600)).c_str()))
                clicked = path;
            ImGui::PopID();
        }
        ImGui::EndPopup();
        if (!clicked.empty())
            OpenRecentFile(clicked);
    }

    ImGui::SetNextWindowPos(moreAnchor, ImGuiCond_Always, ImVec2(1, 0));
    if (ImGui::BeginPopup("##moreMenu"))
    {
        if (ImGui::MenuItem("Exit"))
            exitRequested = true;
        ImGui::EndPopup();
    }
    ImGui::PopStyleVar();
}

void TailView::DrawTabs(ImVec2 size)
{
    const float stripHeight = S(36);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + size.x, origin.y + stripHeight), P().chrome);
    drawList->AddLine(ImVec2(origin.x, origin.y + stripHeight - 1), ImVec2(origin.x + size.x, origin.y + stripHeight - 1),
                      P().border);

    uint32_t closeTabId = 0;
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(S(14), (stripHeight - ImGui::GetFontSize()) * 0.5f));
    ImGui::PushStyleVar(ImGuiStyleVar_ItemInnerSpacing, ImVec2(S(8), S(4)));
    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(0, 0));
    if (ImGui::BeginTabBar("##tabs", ImGuiTabBarFlags_FittingPolicyScroll | ImGuiTabBarFlags_NoTooltip))
    {
        for (Tab& tab : tabs_)
        {
            bool isSelected = tab.id == selectedTabId_;
            ImGui::PushStyleColor(ImGuiCol_Text, isSelected ? P().text : P().textDim);
            bool open = true;
            std::string label = FileNameOf(tab.path) + "###tab" + std::to_string(tab.id);
            ImGuiTabItemFlags flags = tab.id == pendingSelectTabId_ ? ImGuiTabItemFlags_SetSelected : 0;
            bool active = ImGui::BeginTabItem(label.c_str(), &open, flags);
            ImGui::PopStyleColor();

            if (ImGui::IsItemHovered(ImGuiHoveredFlags_ForTooltip))
                ImGui::SetTooltip("%s (%s)", tab.path.c_str(), FormatFileSize(tab.tailer->Length()).c_str());

            if (active)
            {
                ImVec2 min = ImGui::GetItemRectMin(), max = ImGui::GetItemRectMax();
                drawList->AddRectFilled(ImVec2(min.x, max.y - S(2)), ImVec2(max.x, max.y), P().accent);
                ImGui::EndTabItem();

                if (pendingSelectTabId_ == 0 || pendingSelectTabId_ == tab.id)
                {
                    selectedTabId_ = tab.id;
                    pendingSelectTabId_ = 0;
                }
            }
            if (!open)
                closeTabId = tab.id;
        }
        ImGui::EndTabBar();
    }
    ImGui::PopStyleVar(3);

    if (Tab* tab = SelectedTab())
    {
        ImGui::SetCursorScreenPos(ImVec2(origin.x, origin.y + stripHeight));
        ImGui::PushID((int)tab->id);
        tab->view->Draw("##log", ImVec2(size.x, size.y - stripHeight), logTheme_.fontSize, LogBackground(), LogText(),
                        highlightRules_.rules);
        ImGui::PopID();
    }

    if (closeTabId != 0)
    {
        size_t index = 0;
        while (tabs_[index].id != closeTabId)
            index++;
        tabs_.erase(tabs_.begin() + (ptrdiff_t)index);

        if (tabs_.empty())
            selectedTabId_ = pendingSelectTabId_ = 0;
        else if (selectedTabId_ == closeTabId)
            selectedTabId_ = pendingSelectTabId_ = tabs_[ImMin(index, tabs_.size() - 1)].id;
    }
}

void TailView::DrawEmptyState(ImVec2 size)
{
    const Palette& p = P();
    ImVec2 area = ImGui::GetCursorScreenPos();
    ImVec2 panel = S(440, 240);
    ImVec2 min(std::floor(area.x + (size.x - panel.x) * 0.5f), std::floor(area.y + (size.y - panel.y) * 0.5f));
    ImVec2 max(min.x + panel.x, min.y + panel.y);

    // Clicking anywhere in the drop zone opens the file picker.
    ImGui::SetCursorScreenPos(min);
    if (ImGui::InvisibleButton("##emptyState", panel))
        ShowOpenDialog();
    bool hovered = ImGui::IsItemHovered();
    if (hovered)
        ImGui::SetMouseCursor(ImGuiMouseCursor_Hand);

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(min, max, p.card, S(8));
    DashedRoundedRect(drawList, min, max, hovered ? p.accent : p.border, S(8), S(1.5f), S(4), S(3));

    ImFont* font = ImGui::GetFont();
    const float iconSize = S(Theme::Em(36)), headerSize = S(Theme::Em(15)), captionSize = S(Theme::Em(12));
    const float buttonHeight = S(32);
    const char* header = "Drop a log file here to start tailing";
    const char* caption = "New lines appear as they're written";
    const char* buttonLabel = "Open file…";
    const char* shortcut = "Ctrl+O";

    ImVec2 iconExtent = font->CalcTextSizeA(iconSize, FLT_MAX, 0, Icon::Document);
    ImVec2 headerExtent = g_fonts.semibold->CalcTextSizeA(headerSize, FLT_MAX, 0, header);
    ImVec2 captionExtent = font->CalcTextSizeA(captionSize, FLT_MAX, 0, caption);
    float totalHeight = iconExtent.y + S(12) + headerExtent.y + S(8) + captionExtent.y + S(16) + buttonHeight;
    float centerX = (min.x + max.x) * 0.5f;
    float y = std::floor((min.y + max.y - totalHeight) * 0.5f);

    drawList->AddText(font, iconSize, ImVec2(centerX - iconExtent.x / 2, y), p.accent, Icon::Document);
    y += iconExtent.y + S(12);
    drawList->AddText(g_fonts.semibold, headerSize, ImVec2(centerX - headerExtent.x / 2, y), p.text, header);
    y += headerExtent.y + S(8);
    drawList->AddText(font, captionSize, ImVec2(centerX - captionExtent.x / 2, y), p.textDim, caption);
    y += captionExtent.y + S(16);

    // The accent "button" is drawn only; the whole panel is the click target.
    float iconWidth = ImGui::CalcTextSize(Icon::Open).x, labelWidth = ImGui::CalcTextSize(buttonLabel).x;
    float shortcutWidth = ImGui::CalcTextSize(shortcut).x;
    float buttonWidth = S(14) * 2 + iconWidth + S(8) + labelWidth + S(10) + shortcutWidth;
    ImVec2 buttonMin(std::floor(centerX - buttonWidth / 2), y), buttonMax(buttonMin.x + buttonWidth, y + buttonHeight);
    bool buttonHovered = ImGui::IsMouseHoveringRect(buttonMin, buttonMax) && hovered;
    bool buttonHeld = buttonHovered && ImGui::IsMouseDown(ImGuiMouseButton_Left);
    drawList->AddRectFilled(buttonMin, buttonMax,
                            buttonHeld ? p.accentFillPressed : buttonHovered ? p.accentFillHover : p.accentFill, S(4));
    float x = buttonMin.x + S(14), textY = y + (buttonHeight - ImGui::GetFontSize()) * 0.5f;
    drawList->AddText(ImVec2(x, textY), IM_COL32_WHITE, Icon::Open);
    x += iconWidth + S(8);
    drawList->AddText(ImVec2(x, textY), IM_COL32_WHITE, buttonLabel);
    x += labelWidth + S(10);
    drawList->AddText(ImVec2(x, textY), IM_COL32(255, 255, 255, 180), shortcut);
}

void TailView::DrawStatusBar(float width)
{
    const Palette& p = P();
    const float height = S(32);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + width, origin.y + height), p.chrome);
    drawList->AddLine(origin, ImVec2(origin.x + width, origin.y), p.border);

    ImGui::PushFont(nullptr, Theme::CaptionFontSize());
    float textY = origin.y + (height - ImGui::GetFontSize()) * 0.5f;
    float x = origin.x + S(12);
    Tab* tab = SelectedTab();

    // Auto-scroll checkbox on the right.
    float checkboxWidth = S(18) + S(8) + ImGui::CalcTextSize("Auto-scroll").x;
    float right = origin.x + width - S(12) - checkboxWidth;
    ImGui::SetCursorScreenPos(ImVec2(right, origin.y + (height - S(24)) * 0.5f));
    if (Checkbox("Auto-scroll", &autoScroll_) && autoScroll_ && tab)
        tab->view->ScrollToEnd();
    ImGui::SetItemTooltip("Follow new lines as they're written");

    if (!tab)
    {
        drawList->AddText(ImVec2(x, textY), p.textDim, "No file open");
        ImGui::PopFont();
        return;
    }

    drawList->AddCircleFilled(ImVec2(x + S(3.5f), origin.y + height * 0.5f), S(3.5f), p.accent);
    x += S(13);
    drawList->AddText(ImVec2(x, textY), p.text, "Tailing");
    x += ImGui::CalcTextSize("Tailing").x + S(12);

    size_t lines = tab->view->DisplayLineCount();
    std::string stats = FormatFileSize(tab->tailer->Length()) + "  ·  " + FormatThousands(lines)
                        + (lines == 1 ? " line" : " lines");
    float statsWidth = ImGui::CalcTextSize(stats.c_str()).x;
    float statsX = right - S(20) - statsWidth;
    drawList->AddText(ImVec2(statsX, textY), p.textDim, stats.c_str());

    std::string path = EllipsizeStart(tab->path, statsX - S(20) - x);
    drawList->AddText(ImVec2(x, textY), p.textDim, path.c_str());
    ImGui::PopFont();
}

void TailView::DrawHighlightsDialog()
{
    if (!BeginDialog(HighlightsDialogId, S(460)))
        return;

    SectionHeader("Rules");
    Caption("Lines containing a word below are highlighted with its color.");
    ImGui::Dummy(ImVec2(0, S(4)));

    if (highlightRules_.rules.empty())
        TextDim("No highlight rules yet.");

    int removeIndex = -1;
    const float removeWidth = S(20) + ImGui::CalcTextSize(Icon::Delete).x;
    for (size_t i = 0; i < highlightRules_.rules.size(); i++)
    {
        HighlightRule& rule = highlightRules_.rules[i];
        ImGui::PushID((int)i);
        if (ColorSwatch("##color", rule.color))
            highlightsDirty_ = true;

        ImGui::SameLine();
        ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - removeWidth - ImGui::GetStyle().ItemSpacing.x);
        ImGui::InputText("##word", &rule.word);
        if (ImGui::IsItemActivated())
            wordBeforeEdit_ = rule.word;
        if (ImGui::IsItemDeactivatedAfterEdit())
        {
            std::string word = Trim(rule.word);
            rule.word = word.empty() ? wordBeforeEdit_ : word;
            highlightsDirty_ = true;
        }

        ImGui::SameLine();
        if (ToolButton("##remove", Icon::Delete, nullptr))
            removeIndex = (int)i;
        ImGui::SetItemTooltip("Remove rule");
        ImGui::PopID();
    }
    if (removeIndex >= 0)
    {
        highlightRules_.rules.erase(highlightRules_.rules.begin() + removeIndex);
        highlightsDirty_ = true;
    }

    // "Add a rule" card.
    ImGui::Dummy(ImVec2(0, S(4)));
    ImGui::PushStyleColor(ImGuiCol_ChildBg, P().chrome);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(12, 12));
    ImGui::BeginChild("##addRule", ImVec2(0, 0),
                      ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysUseWindowPadding);
    FieldLabel("Add a rule");
    ColorSwatch("##newColor", newRuleColor_);
    ImGui::SameLine();
    float addWidth = ButtonWidth("Add", Icon::Add);
    ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - addWidth - ImGui::GetStyle().ItemSpacing.x);
    bool submitted = ImGui::InputTextWithHint("##newWord", "Word to match", &newRuleWord_,
                                              ImGuiInputTextFlags_EnterReturnsTrue);
    ImGui::SameLine();
    submitted |= Button("Add", Icon::Add);
    if (submitted && !Trim(newRuleWord_).empty())
    {
        highlightRules_.rules.push_back({ Trim(newRuleWord_), newRuleColor_ });
        newRuleWord_.clear();
        highlightsDirty_ = true;
    }
    ImGui::EndChild();
    ImGui::PopStyleVar();
    ImGui::PopStyleColor();

    DialogFooter();
    AlignRight(ButtonWidth("Close"));
    bool close = Button("Close", nullptr, true) || DialogCancelPressed();

    // Save once editing settles, not on every keystroke or color drag.
    if (highlightsDirty_ && (close || !ImGui::IsAnyItemActive()))
    {
        highlightRules_.Save();
        highlightsDirty_ = false;
    }
    if (close)
        ImGui::CloseCurrentPopup();
    ImGui::EndPopup();
}

void TailView::DrawAppearanceDialog()
{
    if (!BeginDialog(AppearanceDialogId, S(420)))
        return;

    Color background = LogBackground(), text = LogText();

    // Live preview of the log pane.
    ImGui::PushStyleColor(ImGuiCol_ChildBg, background.ToU32());
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(12, 10));
    ImGui::BeginChild("##preview", ImVec2(0, 0),
                      ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY | ImGuiChildFlags_AlwaysUseWindowPadding);
    ImGui::PushFont(g_fonts.mono, Theme::MonoEm(logTheme_.fontSize));
    ImGui::PushStyleColor(ImGuiCol_Text, text.ToU32());
    ImGui::TextUnformatted("12:00:01 INFO  Server started on port 8080\n"
                           "12:00:02 WARN  Cache miss for /api/users\n"
                           "12:00:03 INFO  Request completed in 42 ms");
    ImGui::PopStyleColor();
    ImGui::PopFont();
    ImGui::EndChild();
    ImGui::PopStyleVar();
    ImGui::PopStyleColor();

    ImGui::Dummy(ImVec2(0, S(8)));
    SectionHeader("Colors");
    const float column = S(100);

    // Picking either color saves both, so the pane stops following the app theme.
    RowLabel("Background", column);
    bool colorsChanged = ColorSwatch("##background", background);
    RowLabel("Text", column);
    colorsChanged |= ColorSwatch("##text", text);
    if (colorsChanged)
    {
        logTheme_.background = background;
        logTheme_.text = text;
        themeDirty_ = true;
    }

    RowLabel("Presets", column);
    if (Button("Theme"))
    {
        logTheme_.background.reset();
        logTheme_.text.reset();
        themeDirty_ = true;
    }
    ImGui::SetItemTooltip("Follow the app's dark or light theme");
    ImGui::SameLine();
    if (Button("Matrix"))
    {
        logTheme_.background = LogThemeStore::MatrixBackground;
        logTheme_.text = LogThemeStore::MatrixText;
        themeDirty_ = true;
    }
    ImGui::SetItemTooltip("Green text on black");

    if (std::abs(RelativeLuminance(LogBackground()) - RelativeLuminance(LogText())) < 0.15)
        WarningBanner("Low contrast: background and text are hard to tell apart.", ImGui::GetContentRegionAvail().x);

    ImGui::Dummy(ImVec2(0, S(8)));
    SectionHeader("Font");
    RowLabel("Size", column);
    int fontSize = (int)logTheme_.fontSize;
    ImGui::SetNextItemWidth(S(120));
    if (ImGui::InputInt("##fontSize", &fontSize, 1, 1))
    {
        logTheme_.fontSize = ImClamp((float)fontSize, LogThemeStore::MinFontSize, LogThemeStore::MaxFontSize);
        themeDirty_ = true;
    }

    DialogFooter();
    if (Button("Reset to Default"))
    {
        logTheme_.ResetToDefault();
        themeDirty_ = false;
    }
    ImGui::SetItemTooltip("Theme colors and the default font size");
    ImGui::SameLine();
    AlignRight(ButtonWidth("Close"));
    bool close = Button("Close", nullptr, true) || DialogCancelPressed()
                 || (!ImGui::IsAnyItemActive() && ImGui::IsWindowFocused() && ImGui::IsKeyPressed(ImGuiKey_Enter, false));

    if (themeDirty_ && (close || !ImGui::IsAnyItemActive()))
    {
        logTheme_.Save();
        themeDirty_ = false;
    }
    if (close)
        ImGui::CloseCurrentPopup();
    ImGui::EndPopup();
}

void TailView::DrawMessageDialog()
{
    std::string id = messageTitle_ + MessageDialogId;
    if (!BeginDialog(id.c_str(), S(400)))
        return;

    ImGui::PushTextWrapPos(0);
    ImGui::TextUnformatted(messageText_.c_str());
    ImGui::PopTextWrapPos();

    DialogFooter();
    AlignRight(ButtonWidth("Close"));
    if (Button("Close", nullptr, true) || DialogCancelPressed() || ImGui::IsKeyPressed(ImGuiKey_Enter, false))
        ImGui::CloseCurrentPopup();
    ImGui::EndPopup();
}

void TailView::ShowMessage(std::string title, std::string message)
{
    messageTitle_ = std::move(title);
    messageText_ = std::move(message);
    openMessage_ = true;
    Platform::RequestFrames();
}

void TailView::ShowOpenDialog()
{
    // The file picker runs its own message loop, so show it between frames.
    Platform::RunAfterFrame([this] { OpenFiles(Platform::ShowOpenFilesDialog(L"Open Log File")); });
}

void TailView::OpenFiles(const std::vector<std::wstring>& paths)
{
    for (const std::wstring& path : paths)
        if (FileExists(path))
            OpenFile(path);
}

void TailView::OpenRecentFile(const std::string& path)
{
    std::wstring widePath = Widen(path);
    if (FileExists(widePath))
    {
        OpenFile(widePath);
        return;
    }

    recentFiles_.Remove(path);
    ShowMessage("File Not Found", "File not found: " + path);
}

void TailView::OpenFile(const std::wstring& widePath)
{
    std::string path = Narrow(widePath);
    for (const Tab& tab : tabs_)
    {
        if (EqualsIgnoreCase(tab.path, path))
        {
            selectedTabId_ = pendingSelectTabId_ = tab.id;
            return;
        }
    }

    std::string initialText;
    auto tailer = LogTailer::Open(widePath, initialText);
    if (!tailer)
    {
        ShowMessage("Couldn't Open File", "Couldn't read " + path);
        return;
    }

    Tab tab{ nextTabId_++, path, std::move(tailer), std::make_unique<LogView>(std::move(initialText)) };
    tab.view->ScrollToEnd();
    selectedTabId_ = pendingSelectTabId_ = tab.id;
    tabs_.push_back(std::move(tab));

    recentFiles_.Add(path);
    Platform::RequestFrames();
}

bool TailView::Poll()
{
    bool changed = false;
    std::string appended;
    for (Tab& tab : tabs_)
    {
        if (!tab.tailer->ReadAppended(appended))
            continue;

        tab.view->Append(appended);
        if (autoScroll_ && tab.id == selectedTabId_)
            tab.view->ScrollToEnd();
        changed = true;
    }
    return changed;
}
