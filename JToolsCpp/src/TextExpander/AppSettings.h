#pragma once

#include <windows.h>

#include <string>

enum class ExpandMode
{
    /// Expand as soon as the last character of the abbreviation is typed.
    Immediate,

    /// Expand when a trigger key (Space/Tab/Enter) is pressed after the abbreviation.
    TriggerKey,
};

/// User preferences, saved to %APPDATA%\TextExpander\settings.json.
struct AppSettings
{
    ExpandMode mode = ExpandMode::TriggerKey;
    bool triggerOnSpace = true;
    bool triggerOnTab = true;
    bool triggerOnEnter = true;

    /// Whether the trigger key is typed after the expansion (e.g. keep the space).
    bool keepTriggerKey = true;

    static AppSettings Load();
    void Save() const;

    bool IsTriggerKey(int vk) const;

    /// Human-readable description of how expansion is triggered, e.g. "Space or Tab".
    std::string DescribeTriggerKeys() const;
};

/// Settings shared between the UI thread (which edits them) and the keyboard hook thread (which reads them).
class SharedSettings
{
public:
    AppSettings Get() const;

    /// Replaces the settings and saves them.
    void Set(const AppSettings& settings);

    void Load();

private:
    mutable SRWLOCK lock_ = SRWLOCK_INIT;
    AppSettings value_;
};
