#include "StartupManager.h"

#include <windows.h>

static const wchar_t* RunKey = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";

bool StartupManager::IsEnabled() const
{
    return RegGetValueW(HKEY_CURRENT_USER, RunKey, valueName_.c_str(), RRF_RT_REG_SZ, nullptr, nullptr, nullptr)
        == ERROR_SUCCESS;
}

void StartupManager::SetEnabled(bool enabled)
{
    HKEY key;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, RunKey, 0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
        return;

    if (enabled)
    {
        wchar_t exePath[MAX_PATH];
        GetModuleFileNameW(nullptr, exePath, MAX_PATH);
        std::wstring command = L"\"" + std::wstring(exePath) + L"\"";
        if (!arguments_.empty())
            command += L" " + arguments_;
        RegSetValueExW(key, valueName_.c_str(), 0, REG_SZ, (const BYTE*)command.c_str(),
                       (DWORD)((command.size() + 1) * sizeof(wchar_t)));
    }
    else
        RegDeleteValueW(key, valueName_.c_str());

    RegCloseKey(key);
}
