#pragma once

#include "../JTail/TailView.h"
#include "../TextExpander/ExpanderHost.h"
#include "../TextExpander/StartupManager.h"
#include "ExpanderView.h"

#include <memory>
#include <string>

enum class AppTheme
{
    Dark,
    Light,

    /// Follow the Windows light/dark app mode.
    System,
};

/// App-wide preferences, saved to %APPDATA%\JTools\settings.json.
struct JToolsSettings
{
    AppTheme theme = AppTheme::Dark;

    static JToolsSettings Load();
    void Save() const;
};

/// The main window's content: a side bar to switch between the tools, and the tools themselves.
/// Both tools stay alive while hidden, so JTail keeps tailing in the background.
class App
{
public:
    /// Launch argument that starts JTools hidden in the tray (used for "Start with Windows").
    static constexpr const wchar_t* StartInTrayArgument = L"--tray";

    App();

    void Draw();

    /// Called every second to pick up log file changes; returns true if something changed.
    bool Poll() { return tail_.Poll(); }

    void OpenFiles(const std::vector<std::wstring>& paths) { tail_.OpenFiles(paths); }

    /// e.g. "JTools — JTail - app.log"
    std::string WindowTitle() const;

    /// Whether the dark palette applies (resolving "Use Windows setting").
    bool UsesDarkTheme() const;

    /// Set when the user asks to exit from inside the window (JTail's More → Exit).
    bool ExitRequested() const { return tail_.exitRequested; }

    ExpanderHost& Expander() { return expander_; }
    StartupManager& Startup() { return startup_; }

private:
    enum class Tool { Tail, Expander };

    /// Unscaled width of the side bar.
    static constexpr float SideBarWidth = 84;

    void DrawSideBar(float height);

    ExpanderHost expander_;
    StartupManager startup_{ L"JTools", StartInTrayArgument };
    JToolsSettings settings_ = JToolsSettings::Load();
    TailView tail_;
    ExpanderView expanderView_{ expander_ };
    Tool tool_ = Tool::Tail;
};
