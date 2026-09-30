#pragma once

#include "../TextExpander/ExpanderHost.h"

#include "imgui.h"

#include <cstdint>
#include <string>
#include <vector>

/// Add, edit and delete abbreviations, turn expansion on or off, and choose when abbreviations expand.
class ExpanderView
{
public:
    explicit ExpanderView(ExpanderHost& expander);

    /// Draws the view filling the given size at the current cursor position.
    void Draw(ImVec2 size);

private:
    void DrawHeader(float width);
    void DrawList(ImVec2 size);
    void DrawEditor(ImVec2 size);
    void DrawSettingsDialog();

    void RefreshSnippets();
    void Select(const Snippet& snippet);
    void StartNew();
    void SaveCurrent();
    void DeleteCurrent();
    void ShowStatus(std::string message, bool isWarning = false);
    std::string Hint() const;

    ExpanderHost& expander_;
    std::vector<Snippet> snippets_;

    std::string search_;
    uint32_t editingId_ = 0;
    std::string abbreviation_;
    std::string expansion_; // \n line endings while editing
    bool focusAbbreviation_ = false;

    std::string status_;
    bool statusIsWarning_ = false;

    /// Delete asks for a second click instead of a popup: "Delete" → "Click again to delete".
    uint64_t deleteConfirmUntil_ = 0;

    // Settings dialog state, copied from the settings when the dialog opens.
    bool openSettings_ = false;
    AppSettings pendingSettings_;
    std::string settingsError_;
};
