#pragma once

#include <windows.h>

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

struct Snippet
{
    /// Identifies the snippet for this run of the app (not saved).
    uint32_t id = 0;
    std::string abbreviation;
    /// Uses \r\n line endings.
    std::string expansion;
};

struct SnippetMatch
{
    /// Abbreviation length in UTF-16 code units (one backspace each).
    size_t abbreviationLength = 0;
    std::wstring expansion;
};

/// Loads and saves abbreviations to %APPDATA%\TextExpander\snippets.json.
/// The UI thread edits the list while the keyboard hook thread looks up matches, so access is locked.
class SnippetStore
{
public:
    /// @return true if this is the first run (no file existed yet).
    bool Load();

    std::vector<Snippet> Snapshot() const;

    /// Adds (id 0) or updates a snippet and saves the list. Returns the snippet's id, or 0 if saving failed.
    uint32_t Upsert(uint32_t id, const std::string& abbreviation, const std::string& expansion);

    bool Remove(uint32_t id);

    /// Finds the snippet whose abbreviation ends the typed word. The abbreviation must start at a
    /// word boundary, so "brb" matches "brb" and "(brb" but not "abrb". Longest match wins.
    bool FindMatch(std::wstring_view typedWord, SnippetMatch& match) const;

private:
    struct Entry
    {
        Snippet snippet;
        std::wstring abbreviation;
        std::wstring expansion;
    };

    static Entry MakeEntry(uint32_t id, std::string abbreviation, std::string expansion);
    bool SaveLocked() const;

    mutable SRWLOCK lock_ = SRWLOCK_INIT;
    std::vector<Entry> entries_;
    uint32_t nextId_ = 1;
};
