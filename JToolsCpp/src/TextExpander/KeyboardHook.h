#pragma once

#include "AppSettings.h"
#include "SnippetStore.h"

#include <windows.h>

#include <atomic>
#include <string>

/// Watches keystrokes system-wide, tracks the word currently being typed, and swaps an
/// abbreviation for its expansion either as soon as it's typed or when a trigger key is pressed,
/// depending on AppSettings::mode.
///
/// The hook runs on its own thread with its own message loop, so typing stays responsive no matter
/// what the UI thread is doing (Windows removes low-level hooks that respond too slowly).
class KeyboardHook
{
public:
    KeyboardHook(const SnippetStore& store, const SharedSettings& settings);
    ~KeyboardHook();

    /// @return false if the hook couldn't be installed.
    bool Start();

    std::atomic<bool> enabled{ true };

private:
    static DWORD WINAPI ThreadMain(void* parameter);
    static LRESULT CALLBACK HookProc(int code, WPARAM wParam, LPARAM lParam);

    /// @return true if the keystroke should be swallowed.
    bool HandleKeyDown(const KBDLLHOOKSTRUCT& info);
    bool TryExpandOnTrigger(WORD triggerVk, HWND foreground, const AppSettings& settings);
    bool TryExpandImmediately(size_t lastKeyLength, HWND foreground);
    bool FindMatch(HWND foreground, SnippetMatch& match) const;
    std::wstring Translate(const KBDLLHOOKSTRUCT& info, HWND foreground);

    static constexpr size_t MaxBufferLength = 64;

    const SnippetStore& store_;
    const SharedSettings& settings_;
    std::wstring buffer_;
    HWND lastWindow_ = nullptr;
    HHOOK hook_ = nullptr;
    HANDLE thread_ = nullptr;
    DWORD threadId_ = 0;
    HANDLE started_ = nullptr;
};
