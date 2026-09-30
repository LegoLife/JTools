#pragma once

#include <string>

/// Toggles launching an app when the user signs in to Windows.
class StartupManager
{
public:
    /// @param valueName Name of the entry under the Run key; unique per app.
    /// @param arguments Command-line arguments to launch with, e.g. to start hidden in the tray.
    StartupManager(std::wstring valueName, std::wstring arguments)
        : valueName_(std::move(valueName)), arguments_(std::move(arguments))
    {
    }

    bool IsEnabled() const;
    void SetEnabled(bool enabled);

private:
    std::wstring valueName_;
    std::wstring arguments_;
};
