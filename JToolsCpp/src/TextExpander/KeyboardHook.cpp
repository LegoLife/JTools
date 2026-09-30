#include "KeyboardHook.h"

#include "TextSender.h"

namespace
{
    KeyboardHook* g_instance = nullptr;

    bool IsDown(int vk)
    {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    bool IsModifier(DWORD vk)
    {
        return vk == VK_SHIFT || vk == VK_CONTROL || vk == VK_MENU || vk == VK_CAPITAL || vk == VK_LWIN || vk == VK_RWIN
            || (vk >= VK_LSHIFT && vk <= VK_RMENU);
    }

    bool IsOwnWindow(HWND window)
    {
        DWORD processId = 0;
        GetWindowThreadProcessId(window, &processId);
        return processId == GetCurrentProcessId();
    }
}

KeyboardHook::KeyboardHook(const SnippetStore& store, const SharedSettings& settings)
    : store_(store), settings_(settings)
{
}

KeyboardHook::~KeyboardHook()
{
    if (thread_)
    {
        PostThreadMessageW(threadId_, WM_QUIT, 0, 0);
        WaitForSingleObject(thread_, 2000);
        CloseHandle(thread_);
    }
    g_instance = nullptr;
}

bool KeyboardHook::Start()
{
    g_instance = this;
    started_ = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    thread_ = CreateThread(nullptr, 0, ThreadMain, this, 0, &threadId_);
    if (thread_)
        WaitForSingleObject(started_, INFINITE);
    CloseHandle(started_);
    return hook_ != nullptr;
}

DWORD WINAPI KeyboardHook::ThreadMain(void* parameter)
{
    auto* self = (KeyboardHook*)parameter;

    // Create the thread's message queue before signaling, so the quit message can't arrive too early.
    MSG msg;
    PeekMessageW(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
    self->hook_ = SetWindowsHookExW(WH_KEYBOARD_LL, HookProc, GetModuleHandleW(nullptr), 0);
    SetEvent(self->started_);
    if (!self->hook_)
        return 1;

    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
        DispatchMessageW(&msg);

    UnhookWindowsHookEx(self->hook_);
    return 0;
}

LRESULT CALLBACK KeyboardHook::HookProc(int code, WPARAM wParam, LPARAM lParam)
{
    if (code >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) && g_instance)
    {
        const auto& info = *(const KBDLLHOOKSTRUCT*)lParam;
        if (info.dwExtraInfo != TextSender::InjectedMarker && g_instance->HandleKeyDown(info))
            return 1; // swallow the key
    }
    return CallNextHookEx(nullptr, code, wParam, lParam);
}

bool KeyboardHook::HandleKeyDown(const KBDLLHOOKSTRUCT& info)
{
    if (!enabled)
    {
        buffer_.clear();
        return false;
    }

    HWND foreground = GetForegroundWindow();
    if (foreground != lastWindow_)
    {
        buffer_.clear();
        lastWindow_ = foreground;
    }

    DWORD vk = info.vkCode;
    if (IsModifier(vk))
        return false;

    if (IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
    {
        buffer_.clear();
        return false;
    }

    AppSettings settings = settings_.Get();
    switch (vk)
    {
    case VK_BACK:
        if (!buffer_.empty())
            buffer_.pop_back();
        return false;
    case VK_SPACE:
    case VK_TAB:
    case VK_RETURN:
        if (settings.mode == ExpandMode::TriggerKey && settings.IsTriggerKey((int)vk))
            return TryExpandOnTrigger((WORD)vk, foreground, settings);
        buffer_.clear(); // whitespace ends the word
        return false;
    }

    std::wstring typed = Translate(info, foreground);
    if (typed.empty())
    {
        // Arrows, Home/End, Delete, F-keys, etc. move the caret, so the buffer no longer
        // reflects what's in front of it.
        buffer_.clear();
        return false;
    }

    buffer_ += typed;
    if (buffer_.size() > MaxBufferLength)
        buffer_.erase(0, buffer_.size() - MaxBufferLength);

    if (settings.mode == ExpandMode::Immediate)
        return TryExpandImmediately(typed.size(), foreground);
    return false;
}

bool KeyboardHook::TryExpandOnTrigger(WORD triggerVk, HWND foreground, const AppSettings& settings)
{
    SnippetMatch match;
    bool found = FindMatch(foreground, match);
    buffer_.clear();
    if (!found)
        return false;

    // The trigger key is swallowed, so everything typed so far is on screen.
    TextSender::ReplaceAsync((int)match.abbreviationLength, std::move(match.expansion),
                             settings.keepTriggerKey ? triggerVk : 0);
    return true;
}

bool KeyboardHook::TryExpandImmediately(size_t lastKeyLength, HWND foreground)
{
    SnippetMatch match;
    if (!FindMatch(foreground, match))
        return false;

    buffer_.clear();
    // The keystroke that completed the abbreviation is swallowed, so it never reaches the screen.
    int onScreen = match.abbreviationLength > lastKeyLength ? (int)(match.abbreviationLength - lastKeyLength) : 0;
    TextSender::ReplaceAsync(onScreen, std::move(match.expansion), 0);
    return true;
}

bool KeyboardHook::FindMatch(HWND foreground, SnippetMatch& match) const
{
    // Don't expand inside our own windows, or you couldn't type abbreviations there.
    if (buffer_.empty() || IsOwnWindow(foreground))
        return false;
    return store_.FindMatch(buffer_, match);
}

std::wstring KeyboardHook::Translate(const KBDLLHOOKSTRUCT& info, HWND foreground)
{
    BYTE keyState[256] = {};
    if (IsDown(VK_SHIFT))
        keyState[VK_SHIFT] = 0x80;
    if (GetKeyState(VK_CAPITAL) & 1)
        keyState[VK_CAPITAL] = 0x01;

    HKL layout = GetKeyboardLayout(GetWindowThreadProcessId(foreground, nullptr));
    wchar_t translated[8];
    // Flag 0x4: don't change the keyboard state (keeps dead keys working in the target app).
    int count = ToUnicodeEx(info.vkCode, info.scanCode, keyState, translated, 8, 0x4, layout);
    if (count <= 0)
        return {};

    std::wstring result(translated, count);
    for (wchar_t c : result)
        if (c < 0x20 || (c >= 0x7F && c <= 0x9F))
            return {};
    return result;
}
