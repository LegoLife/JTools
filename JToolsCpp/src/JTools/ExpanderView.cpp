#include "ExpanderView.h"

#include "../Common/Theme.h"
#include "../Common/Util.h"
#include "../Common/Widgets.h"
#include "imgui_internal.h"
#include "imgui_stdlib.h"

#include <algorithm>
#include <cfloat>

using namespace Widgets;

namespace
{
    const char* SettingsDialogId = "Text Expansion Settings";
    constexpr uint64_t DeleteConfirmWindowMs = 3000;
    constexpr size_t MaxPreviewLength = 80;

    const Palette& P()
    {
        return Theme::Current();
    }

    std::string Preview(const std::string& expansion)
    {
        std::string singleLine = ReplaceLineEndings(expansion, " ⏎ ");
        if (singleLine.size() > MaxPreviewLength * 4) // UTF-8; the width limit trims it further
            singleLine.resize(MaxPreviewLength * 4);
        return singleLine;
    }

    bool ContainsWhitespace(const std::string& s)
    {
        return std::any_of(s.begin(), s.end(), [](char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n'; });
    }

    /// A rounded panel with the given fill color and padding (sized to its content vertically).
    bool BeginCard(const char* id, ImU32 fill)
    {
        ImGui::PushStyleColor(ImGuiCol_ChildBg, fill);
        ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(14, 10));
        bool visible = ImGui::BeginChild(id, ImVec2(0, 0),
                                         ImGuiChildFlags_Borders | ImGuiChildFlags_AutoResizeY
                                             | ImGuiChildFlags_AlwaysUseWindowPadding);
        ImGui::PopStyleVar();
        ImGui::PopStyleColor();
        return visible;
    }
}

ExpanderView::ExpanderView(ExpanderHost& expander) : expander_(expander)
{
    RefreshSnippets();
    if (!snippets_.empty())
        Select(snippets_.front());
    else
        StartNew();
}

void ExpanderView::RefreshSnippets()
{
    snippets_ = expander_.store.Snapshot();
    std::sort(snippets_.begin(), snippets_.end(), [](const Snippet& a, const Snippet& b) {
        return CompareIgnoreCase(a.abbreviation, b.abbreviation) < 0;
    });
}

std::string ExpanderView::Hint() const
{
    AppSettings settings = expander_.settings.Get();
    std::string howToExpand = settings.mode == ExpandMode::Immediate
        ? "Type an abbreviation in any app and it expands immediately."
        : "Type an abbreviation in any app, then press " + settings.DescribeTriggerKeys() + " to expand it.";
    return howToExpand + " Abbreviations are case-sensitive and can't contain spaces. "
                         "Expansion is paused while JTools is the active window.";
}

void ExpanderView::Draw(ImVec2 size)
{
    const float headerHeight = S(56), listWidth = S(300);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    bool available = expander_.IsAvailable();

    DrawHeader(size.x);
    float top = origin.y + headerHeight;

    // If another process owns expansion it also owns the abbreviation file, so don't edit it here.
    if (!available)
    {
        ImGui::SetCursorScreenPos(ImVec2(origin.x + S(16), top + S(16)));
        WarningBanner("The standalone Text Expander app is running, so it is handling expansion. Exit it from its tray "
                      "icon and restart JTools to manage abbreviations here.",
                      size.x - S(32), true);
        top = ImGui::GetItemRectMax().y + S(8);
    }

    ImGui::BeginDisabled(!available);
    ImGui::SetCursorScreenPos(ImVec2(origin.x, top));
    DrawList(ImVec2(listWidth, origin.y + size.y - top));
    ImGui::SetCursorScreenPos(ImVec2(origin.x + listWidth, top));
    DrawEditor(ImVec2(size.x - listWidth, origin.y + size.y - top));
    ImGui::EndDisabled();

    bool popupOpen = ImGui::IsPopupOpen(nullptr, ImGuiPopupFlags_AnyPopupId | ImGuiPopupFlags_AnyPopupLevel);
    if (available && !popupOpen)
    {
        if (ImGui::Shortcut(ImGuiMod_Ctrl | ImGuiKey_S, ImGuiInputFlags_RouteGlobal))
            SaveCurrent();
        if (ImGui::Shortcut(ImGuiMod_Ctrl | ImGuiKey_N, ImGuiInputFlags_RouteGlobal))
            StartNew();
    }

    if (openSettings_)
    {
        ImGui::OpenPopup(SettingsDialogId);
        openSettings_ = false;
    }
    DrawSettingsDialog();
}

void ExpanderView::DrawHeader(float width)
{
    const float height = S(56);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + width, origin.y + height), P().chrome);
    drawList->AddLine(ImVec2(origin.x, origin.y + height - 1), ImVec2(origin.x + width, origin.y + height - 1), P().border);

    const float titleSize = S(Theme::Em(20));
    drawList->AddText(g_fonts.semibold, titleSize, ImVec2(origin.x + S(20), origin.y + (height - titleSize) * 0.5f - S(1)),
                      P().text, "Text Expander");

    bool enabled = expander_.Enabled();
    const char* switchLabel = enabled ? "Expansion on" : "Expansion off";
    float settingsWidth = S(20) + ImGui::CalcTextSize(Icon::Settings).x + S(6) + ImGui::CalcTextSize("Settings").x;
    float switchWidth = S(40) + S(10) + ImGui::CalcTextSize("Expansion off").x;
    float right = origin.x + width - S(12);

    ImGui::BeginDisabled(!expander_.IsAvailable());
    ImGui::SetCursorScreenPos(ImVec2(right - settingsWidth - S(16) - switchWidth, origin.y + (height - S(28)) * 0.5f));
    if (ToggleSwitch("##enabled", &enabled, switchLabel))
        expander_.SetEnabled(enabled);

    ImGui::SetCursorScreenPos(ImVec2(right - settingsWidth, origin.y + (height - S(32)) * 0.5f));
    if (ToolButton("##settings", Icon::Settings, "Settings"))
    {
        pendingSettings_ = expander_.settings.Get();
        settingsError_.clear();
        openSettings_ = true;
    }
    ImGui::SetItemTooltip("When abbreviations expand");
    ImGui::EndDisabled();
}

void ExpanderView::DrawList(ImVec2 size)
{
    const Palette& p = P();
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + size.x, origin.y + size.y), p.chrome);
    drawList->AddLine(ImVec2(origin.x + size.x - 1, origin.y), ImVec2(origin.x + size.x - 1, origin.y + size.y), p.border);

    const float padding = S(12), innerWidth = size.x - padding * 2;

    // Search box with a magnifier drawn inside its left edge.
    ImGui::SetCursorScreenPos(ImVec2(origin.x + padding, origin.y + padding));
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(S(32), ImGui::GetStyle().FramePadding.y));
    ImGui::SetNextItemWidth(innerWidth);
    ImGui::InputTextWithHint("##search", "Search abbreviations", &search_);
    ImGui::PopStyleVar();
    ImVec2 searchMin = ImGui::GetItemRectMin(), searchMax = ImGui::GetItemRectMax();
    const float iconSize = S(Theme::Em(12));
    drawList->AddText(ImGui::GetFont(), iconSize,
                      ImVec2(searchMin.x + S(11), (searchMin.y + searchMax.y - iconSize) * 0.5f), Col(p.textDim), Icon::Search);

    // New / Delete along the bottom.
    const float buttonHeight = S(32), gap = S(8);
    float buttonsTop = origin.y + size.y - padding - buttonHeight;
    float buttonWidth = (innerWidth - gap) * 0.5f;
    ImGui::SetCursorScreenPos(ImVec2(origin.x + padding, buttonsTop));
    if (Button("New", Icon::Add, true, buttonWidth))
        StartNew();
    ImGui::SetItemTooltip("Ctrl+N");
    ImGui::SameLine(0, gap);
    bool confirming = NowMs() < deleteConfirmUntil_;
    ImGui::BeginDisabled(editingId_ == 0);
    if (Button(confirming ? "Click again to delete###delete" : "Delete###delete", nullptr, false, buttonWidth))
    {
        if (confirming)
            DeleteCurrent();
        else
            deleteConfirmUntil_ = NowMs() + DeleteConfirmWindowMs;
    }
    ImGui::EndDisabled();

    // The abbreviation list fills the space between.
    float listTop = searchMax.y + S(8);
    ImGui::SetCursorScreenPos(ImVec2(origin.x + padding, listTop));
    ImGui::BeginChild("##snippets", ImVec2(innerWidth, buttonsTop - S(8) - listTop));

    ImFont* font = ImGui::GetFont();
    const float monoSize = S(Theme::MonoEm(14)), captionSize = S(Theme::CaptionFontSize());
    const float monoLine = ImMax(monoSize, S(14) * 1.3f), rowPadding = S(7), lineGap = S(1);
    float rowHeight = rowPadding * 2 + monoLine + lineGap + captionSize;
    std::string filter = Trim(search_);

    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(0, S(2)));
    for (const Snippet& snippet : snippets_)
    {
        if (!filter.empty() && !ContainsIgnoreCase(snippet.abbreviation, filter)
            && !ContainsIgnoreCase(snippet.expansion, filter))
            continue;

        ImGui::PushID((int)snippet.id);
        ImVec2 min = ImGui::GetCursorScreenPos();
        float width = ImGui::GetContentRegionAvail().x;
        ImVec2 max(min.x + width, min.y + rowHeight);
        if (ImGui::InvisibleButton("##row", ImVec2(width, rowHeight)))
            Select(snippet);
        bool selected = snippet.id == editingId_;
        bool hovered = ImGui::IsItemHovered();

        ImDrawList* rowDrawList = ImGui::GetWindowDrawList();
        if (selected || hovered)
            rowDrawList->AddRectFilled(min, max, Col(selected ? (hovered ? p.selectionHover : p.selection) : p.surface), S(4));

        float x = min.x + S(10), y = min.y + rowPadding;
        rowDrawList->AddText(g_fonts.mono, monoSize, ImVec2(x, y), Col(p.text), snippet.abbreviation.c_str());
        y += monoLine + lineGap;

        ImGui::PushFont(nullptr, Theme::CaptionFontSize());
        std::string preview = EllipsizeEnd(Preview(snippet.expansion), width - S(20));
        ImGui::PopFont();
        rowDrawList->AddText(font, captionSize, ImVec2(x, y), Col(p.textDim), preview.c_str());
        ImGui::PopID();
    }
    ImGui::PopStyleVar();
    ImGui::EndChild();
}

void ExpanderView::DrawEditor(ImVec2 size)
{
    const Palette& p = P();
    ImVec2 origin = ImGui::GetCursorScreenPos();
    const float margin = S(16), padding = S(16);
    ImVec2 cardMin(origin.x + margin, origin.y + margin);
    ImVec2 cardMax(origin.x + size.x - margin, origin.y + size.y - margin);
    float cardWidth = cardMax.x - cardMin.x, innerWidth = cardWidth - padding * 2;

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(cardMin, cardMax, p.card, S(8));
    drawList->AddRect(cardMin, cardMax, p.border, S(8), S(1));

    // Footer: hint on the left, status and Save on the right.
    float saveWidth = ButtonWidth("Save", Icon::Save);
    float statusWidth = status_.empty() ? 0 : ImGui::CalcTextSize(status_.c_str()).x + S(12);
    float hintWrap = innerWidth - saveWidth - statusWidth - S(16);
    std::string hint = Hint();
    ImVec2 hintSize = ImGui::GetFont()->CalcTextSizeA(S(Theme::CaptionFontSize()), FLT_MAX, hintWrap, hint.c_str());
    float footerHeight = ImMax(hintSize.y, S(32)) + S(20);
    float footerTop = cardMax.y - footerHeight;
    drawList->AddLine(ImVec2(cardMin.x, footerTop), ImVec2(cardMax.x, footerTop), p.border, S(1));

    drawList->AddText(ImGui::GetFont(), S(Theme::CaptionFontSize()),
                      ImVec2(cardMin.x + padding, footerTop + (footerHeight - hintSize.y) * 0.5f), Col(p.textDim),
                      hint.c_str(), nullptr, hintWrap);

    float buttonY = footerTop + (footerHeight - S(32)) * 0.5f;
    if (!status_.empty())
        drawList->AddText(ImVec2(cardMax.x - padding - saveWidth - statusWidth,
                                 buttonY + (S(32) - ImGui::GetFontSize()) * 0.5f),
                          Col(statusIsWarning_ ? p.warningText : p.text), status_.c_str());
    ImGui::SetCursorScreenPos(ImVec2(cardMax.x - padding - saveWidth, buttonY));
    if (Button("Save", Icon::Save, true))
        SaveCurrent();
    ImGui::SetItemTooltip("Ctrl+S");

    // Fields.
    ImGui::SetCursorScreenPos(ImVec2(cardMin.x + padding, cardMin.y + padding));
    ImGui::BeginGroup();
    FieldLabel("Abbreviation");
    ImGui::SetNextItemWidth(ImClamp(innerWidth, S(240), S(360)));
    ImGui::PushFont(g_fonts.mono, Theme::MonoEm(14));
    if (focusAbbreviation_)
    {
        ImGui::SetKeyboardFocusHere();
        focusAbbreviation_ = false;
    }
    if (ImGui::InputTextWithHint("##abbreviation", "e.g. ;sig", &abbreviation_))
        status_.clear();
    ImGui::PopFont();

    ImGui::Dummy(ImVec2(0, S(8)));
    FieldLabel("Expands to");
    float expansionHeight = footerTop - padding - ImGui::GetCursorScreenPos().y;
    if (ImGui::InputTextMultiline("##expansion", &expansion_, ImVec2(innerWidth, ImMax(expansionHeight, S(60))),
                                  ImGuiInputTextFlags_AllowTabInput | ImGuiInputTextFlags_WordWrap))
        status_.clear();
    ImGui::EndGroup();
}

void ExpanderView::DrawSettingsDialog()
{
    if (!BeginDialog(SettingsDialogId, S(540)))
        return;

    AppSettings& s = pendingSettings_;
    SectionHeader("When should abbreviations expand?");

    if (BeginCard("##immediate", P().chrome))
    {
        if (RadioButton("Immediately after the abbreviation is typed", s.mode == ExpandMode::Immediate))
            s.mode = ExpandMode::Immediate;
        ImGui::Indent(S(28));
        Caption("Tip: start abbreviations with a symbol like ; (e.g. ;sig) so they don't fire while you're typing "
                "ordinary words. If one abbreviation is the start of another (e.g. ;ad and ;addr), the shorter one "
                "always wins.");
        ImGui::Unindent(S(28));
    }
    ImGui::EndChild();

    if (BeginCard("##trigger", P().chrome))
    {
        if (RadioButton("When a trigger key is pressed after the abbreviation", s.mode == ExpandMode::TriggerKey))
            s.mode = ExpandMode::TriggerKey;
        ImGui::Indent(S(28));
        ImGui::BeginDisabled(s.mode != ExpandMode::TriggerKey);
        Checkbox("Space", &s.triggerOnSpace);
        ImGui::SameLine(0, S(20));
        Checkbox("Tab", &s.triggerOnTab);
        ImGui::SameLine(0, S(20));
        Checkbox("Enter", &s.triggerOnEnter);
        Checkbox("Also type the trigger key after the expansion (e.g. keep the space)", &s.keepTriggerKey);
        ImGui::EndDisabled();
        ImGui::Unindent(S(28));
    }
    ImGui::EndChild();

    if (!settingsError_.empty())
        WarningBanner(settingsError_.c_str(), ImGui::GetContentRegionAvail().x);

    DialogFooter();
    float saveWidth = ButtonWidth("Save"), cancelWidth = ButtonWidth("Cancel");
    AlignRight(saveWidth + ImGui::GetStyle().ItemSpacing.x + cancelWidth);
    bool save = Button("Save", nullptr, true)
                || (ImGui::IsWindowFocused() && !ImGui::IsAnyItemActive() && ImGui::IsKeyPressed(ImGuiKey_Enter, false));
    ImGui::SameLine();
    bool cancel = Button("Cancel") || DialogCancelPressed();

    if (save)
    {
        if (s.mode == ExpandMode::TriggerKey && !s.triggerOnSpace && !s.triggerOnTab && !s.triggerOnEnter)
            settingsError_ = "Choose at least one trigger key.";
        else
        {
            expander_.settings.Set(s);
            ImGui::CloseCurrentPopup();
        }
    }
    else if (cancel)
        ImGui::CloseCurrentPopup();
    ImGui::EndPopup();
}

void ExpanderView::Select(const Snippet& snippet)
{
    // An input that has focus keeps its own copy of the text, so release it before swapping the text.
    ImGui::ClearActiveID();
    editingId_ = snippet.id;
    abbreviation_ = snippet.abbreviation;
    expansion_ = ReplaceLineEndings(snippet.expansion, "\n");
    deleteConfirmUntil_ = 0;
    status_.clear();
}

void ExpanderView::StartNew()
{
    ImGui::ClearActiveID();
    editingId_ = 0;
    abbreviation_.clear();
    expansion_.clear();
    deleteConfirmUntil_ = 0;
    status_.clear();
    focusAbbreviation_ = true;
}

void ExpanderView::SaveCurrent()
{
    if (!expander_.IsAvailable())
        return;

    std::string abbreviation = Trim(abbreviation_);
    bool inUse = std::any_of(snippets_.begin(), snippets_.end(), [&](const Snippet& s) {
        return s.id != editingId_ && s.abbreviation == abbreviation;
    });

    const char* problem = abbreviation.empty() ? "Enter an abbreviation."
        : ContainsWhitespace(abbreviation)     ? "Abbreviations can't contain spaces."
        : expansion_.empty()                   ? "Enter the text this abbreviation expands to."
                                               : nullptr;
    if (problem)
    {
        ShowStatus(problem, true);
        return;
    }
    if (inUse)
    {
        ShowStatus("\"" + abbreviation + "\" is already in use.", true);
        return;
    }

    uint32_t id = expander_.store.Upsert(editingId_, abbreviation, ReplaceLineEndings(expansion_, "\r\n"));
    if (id == 0)
    {
        ShowStatus("Couldn't save the abbreviations file.", true);
        return;
    }
    editingId_ = id;
    abbreviation_ = abbreviation;
    RefreshSnippets();
    ShowStatus("Saved ✓");
}

void ExpanderView::DeleteCurrent()
{
    if (editingId_ == 0)
        return;

    bool saved = expander_.store.Remove(editingId_);
    StartNew();
    RefreshSnippets();
    ShowStatus(saved ? "Deleted" : "Couldn't save the abbreviations file.", !saved);
}

void ExpanderView::ShowStatus(std::string message, bool isWarning)
{
    status_ = std::move(message);
    statusIsWarning_ = isWarning;
}
