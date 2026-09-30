#pragma once

#include "../Common/Color.h"

#include <optional>
#include <string>
#include <vector>

/// A single "lines containing this word get this background color" rule.
struct HighlightRule
{
    std::string word;
    Color color;
};

/// Persists the user's line-highlight rules (word + color) as JSON in %AppData%\JTail\highlights.json.
/// Persisting is a convenience; a failed write just means the rules reset next launch.
struct HighlightRulesStore
{
    std::vector<HighlightRule> rules;

    static HighlightRulesStore Load();
    void Save() const;
};

/// Persists the user's chosen log pane background/text colors as JSON in %AppData%\JTail\theme.json.
/// A missing color means "not customized": the log pane uses the app theme's colors and follows
/// dark/light switches.
struct LogThemeStore
{
    static constexpr float DefaultFontSize = 13;
    static constexpr float MinFontSize = 8;
    static constexpr float MaxFontSize = 32;

    /// The original green-on-black look, offered as a preset.
    static constexpr Color MatrixBackground = Color::FromRgb(0x000000);
    static constexpr Color MatrixText = Color::FromRgb(0x33FF66);

    std::optional<Color> background;
    std::optional<Color> text;
    float fontSize = DefaultFontSize;

    static LogThemeStore Load();
    void Save() const;

    /// Clears custom colors (back to following the app theme) and resets the font size.
    void ResetToDefault();
};

/// Persists the most-recently-opened file paths as JSON in %AppData%\JTail\recent.json.
struct RecentFilesStore
{
    static constexpr size_t MaxEntries = 10;

    std::vector<std::string> paths;

    static RecentFilesStore Load();

    /// Moves the path to the front of the list, adding it if new, and persists the result.
    void Add(const std::string& path);

    /// Removes a path that turned out not to exist on disk, and persists the result.
    void Remove(const std::string& path);

private:
    void Save() const;
};
