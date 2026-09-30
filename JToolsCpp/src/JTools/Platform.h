#pragma once

#include <functional>
#include <string>
#include <vector>

/// Services the Win32 host (main.cpp) provides to the UI code.
namespace Platform
{
    /// Runs an action between frames, e.g. a modal Windows dialog that runs its own message loop.
    void RunAfterFrame(std::function<void()> action);

    /// Renders a few more frames even without input (e.g. after state changed outside of ImGui).
    void RequestFrames();

    /// Shows the Windows file picker; returns the chosen files (none if cancelled).
    std::vector<std::wstring> ShowOpenFilesDialog(const wchar_t* title);
}
