#pragma once

#include "LogTailer.h"
#include "LogView.h"
#include "Stores.h"

#include "imgui.h"

#include <memory>
#include <string>
#include <vector>

/// JTail's complete UI (toolbar, file tabs, log views, status bar, and its dialogs).
class TailView
{
public:
    static constexpr const char* DefaultTitle = "JTail";

    TailView();

    /// Draws the view filling the given size at the current cursor position.
    void Draw(ImVec2 size);

    /// Reads anything appended to the open files; returns true if a view changed.
    bool Poll();

    void OpenFiles(const std::vector<std::wstring>& paths);

    /// Window title describing the current state, e.g. "JTail - app.log".
    std::string Title() const;

    /// Set when the user chooses Exit from the toolbar's "More" menu; the host decides what exiting means.
    bool exitRequested = false;

private:
    struct Tab
    {
        uint32_t id;
        std::string path;
        std::unique_ptr<LogTailer> tailer;
        std::unique_ptr<LogView> view;
    };

    void DrawToolbar(float width);
    void DrawTabs(ImVec2 size);
    void DrawEmptyState(ImVec2 size);
    void DrawStatusBar(float width);
    void DrawHighlightsDialog();
    void DrawAppearanceDialog();
    void DrawMessageDialog();

    void ShowOpenDialog();
    void OpenFile(const std::wstring& path);
    void OpenRecentFile(const std::string& path);
    void ShowMessage(std::string title, std::string message);
    Tab* SelectedTab();

    Color LogBackground() const;
    Color LogText() const;

    RecentFilesStore recentFiles_ = RecentFilesStore::Load();
    LogThemeStore logTheme_ = LogThemeStore::Load();
    HighlightRulesStore highlightRules_ = HighlightRulesStore::Load();

    std::vector<Tab> tabs_;
    uint32_t nextTabId_ = 1;
    uint32_t selectedTabId_ = 0;
    uint32_t pendingSelectTabId_ = 0;
    bool autoScroll_ = true;

    // Dialog state.
    bool openHighlights_ = false, openAppearance_ = false, openMessage_ = false;
    bool highlightsDirty_ = false, themeDirty_ = false;
    std::string newRuleWord_;
    Color newRuleColor_ = Color::FromRgb(0xE5534B, 0x80);
    std::string wordBeforeEdit_;
    std::string messageTitle_, messageText_;
};
