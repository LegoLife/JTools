#pragma once

#include "AppSettings.h"
#include "KeyboardHook.h"
#include "SnippetStore.h"

#include <memory>

/// Owns the text-expansion engine for the app: the abbreviation list, settings, and keyboard hook.
/// The hook only runs if no other process (e.g. the standalone Text Expander) is already expanding.
class ExpanderHost
{
public:
    ExpanderHost();
    ~ExpanderHost();

    SnippetStore store;
    SharedSettings settings;

    /// False when another process holds the expander, so expansion can't run here.
    bool IsAvailable() const { return hook_ != nullptr; }

    bool Enabled() const { return hook_ && hook_->enabled; }
    void SetEnabled(bool enabled);

private:
    /// Ensures only one process on the desktop expands text at a time, since two keyboard hooks
    /// would both expand every abbreviation.
    HANDLE lock_ = nullptr;
    std::unique_ptr<KeyboardHook> hook_;
};
