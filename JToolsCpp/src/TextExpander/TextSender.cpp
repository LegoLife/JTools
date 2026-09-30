#include "TextSender.h"

#include <cstring>
#include <vector>

namespace
{
    /// How long to wait for the target app to read the clipboard before restoring it.
    constexpr DWORD ClipboardRestoreDelayMs = 600;

    /// Pause between steps so the target app processes each one before the next arrives.
    constexpr DWORD KeyDelayMs = 20;

    /// Pause after the keystroke that triggered the expansion. Backspaces injected within ~15 ms of it
    /// are dropped by some apps (e.g. Windows 11 Notepad), leaving the abbreviation in place.
    constexpr DWORD TriggerSettleMs = 50;

    /// How long Ctrl+V is held down before both keys are released.
    constexpr DWORD ChordHoldMs = 50;

    HWND g_clipboardOwner = nullptr;

    /// One replacement at a time, so overlapping expansions don't fight over the clipboard.
    SRWLOCK g_sendLock = SRWLOCK_INIT;

    struct ClipboardItem
    {
        UINT format;
        std::vector<BYTE> data;
    };

    struct Job
    {
        int backspaces;
        std::wstring expansion;
        WORD trailingVk;
    };

    bool OpenClipboardWithRetry()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(g_clipboardOwner))
                return true;
            Sleep(50);
        }
        return false;
    }

    /// Formats whose handles are GDI objects or private data rather than global memory, so they can't be copied.
    bool IsCopyableFormat(UINT format)
    {
        switch (format)
        {
        case CF_BITMAP:
        case CF_METAFILEPICT:
        case CF_PALETTE:
        case CF_ENHMETAFILE:
        case CF_OWNERDISPLAY:
        case CF_DSPBITMAP:
        case CF_DSPMETAFILEPICT:
        case CF_DSPENHMETAFILE:
            return false;
        default:
            return !(format >= CF_PRIVATEFIRST && format <= CF_PRIVATELAST)
                && !(format >= CF_GDIOBJFIRST && format <= CF_GDIOBJLAST);
        }
    }

    /// Copies every format currently on the clipboard so it can be put back afterwards.
    std::vector<ClipboardItem> SnapshotClipboard()
    {
        std::vector<ClipboardItem> items;
        if (!OpenClipboardWithRetry())
            return items;

        for (UINT format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
        {
            if (!IsCopyableFormat(format))
                continue;
            // Some formats (e.g. delay-rendered ones) can't be read back; skip them.
            HANDLE handle = GetClipboardData(format);
            SIZE_T size = handle ? GlobalSize(handle) : 0;
            const void* data = size ? GlobalLock(handle) : nullptr;
            if (!data)
                continue;
            items.push_back({ format, std::vector<BYTE>((const BYTE*)data, (const BYTE*)data + size) });
            GlobalUnlock(handle);
        }
        CloseClipboard();
        return items;
    }

    bool SetClipboard(const std::vector<ClipboardItem>& items)
    {
        if (!OpenClipboardWithRetry())
            return false;

        EmptyClipboard();
        bool ok = true;
        for (const ClipboardItem& item : items)
        {
            HGLOBAL memory = GlobalAlloc(GMEM_MOVEABLE, item.data.size());
            void* target = memory ? GlobalLock(memory) : nullptr;
            if (!target)
            {
                ok = false;
                continue;
            }
            memcpy(target, item.data.data(), item.data.size());
            GlobalUnlock(memory);
            if (!SetClipboardData(item.format, memory))
            {
                GlobalFree(memory);
                ok = false;
            }
        }
        CloseClipboard();
        return ok;
    }

    std::vector<ClipboardItem> CreateTextData(const std::wstring& text)
    {
        auto bytes = (const BYTE*)text.c_str();
        ClipboardItem unicodeText{ CF_UNICODETEXT, std::vector<BYTE>(bytes, bytes + (text.size() + 1) * sizeof(wchar_t)) };

        // Keep expansions out of Windows clipboard history (Win+V) and cloud clipboard sync.
        static UINT excludeFormat = RegisterClipboardFormatW(L"ExcludeClipboardContentFromMonitorProcessing");
        ClipboardItem exclude{ excludeFormat, std::vector<BYTE>(4, 0) };
        return { unicodeText, exclude };
    }

    /// Includes the hardware scan code, which some apps read instead of the virtual-key code.
    INPUT Keyboard(WORD vk, DWORD flags)
    {
        INPUT input{};
        input.type = INPUT_KEYBOARD;
        input.ki.wVk = vk;
        input.ki.wScan = (WORD)MapVirtualKeyW(vk, MAPVK_VK_TO_VSC);
        input.ki.dwFlags = flags;
        input.ki.dwExtraInfo = TextSender::InjectedMarker;
        return input;
    }

    void AddKey(std::vector<INPUT>& inputs, WORD vk)
    {
        inputs.push_back(Keyboard(vk, 0));
        inputs.push_back(Keyboard(vk, KEYEVENTF_KEYUP));
    }

    void Send(std::vector<INPUT> inputs)
    {
        if (!inputs.empty())
            SendInput((UINT)inputs.size(), inputs.data(), sizeof(INPUT));
    }

    std::wstring WithCrLf(const std::wstring& text)
    {
        std::wstring result;
        for (size_t i = 0; i < text.size(); i++)
        {
            if (text[i] == L'\r' && i + 1 < text.size() && text[i + 1] == L'\n')
                continue; // the \n adds the pair
            if (text[i] == L'\n' || text[i] == L'\r')
                result += L"\r\n";
            else
                result += text[i];
        }
        return result;
    }

    void Replace(const Job& job)
    {
        auto saved = SnapshotClipboard();
        if (!SetClipboard(CreateTextData(WithCrLf(job.expansion))))
            return; // Clipboard is locked by another app; leave the abbreviation as typed.
        Sleep(TriggerSettleMs);

        std::vector<INPUT> erase;
        for (int i = 0; i < job.backspaces; i++)
            AddKey(erase, VK_BACK);
        // If Shift is still physically held (e.g. from typing a capital letter in the abbreviation),
        // Ctrl+V would become Ctrl+Shift+V, which some apps treat as "paste as plain text" or ignore.
        if (GetAsyncKeyState(VK_SHIFT) & 0x8000)
            erase.push_back(Keyboard(VK_SHIFT, KEYEVENTF_KEYUP));
        Send(erase);
        Sleep(KeyDelayMs);

        // Hold Ctrl+V down briefly. If the whole chord is sent in one burst, apps that check the
        // live Ctrl state when handling V (e.g. Windows 11 Notepad) see Ctrl already released
        // and type a plain "v" instead of pasting.
        Send({ Keyboard(VK_CONTROL, 0), Keyboard('V', 0) });
        Sleep(ChordHoldMs);
        Send({ Keyboard('V', KEYEVENTF_KEYUP), Keyboard(VK_CONTROL, KEYEVENTF_KEYUP) });

        if (job.trailingVk)
        {
            Sleep(KeyDelayMs);
            std::vector<INPUT> trailing;
            AddKey(trailing, job.trailingVk);
            Send(trailing);
        }

        Sleep(ClipboardRestoreDelayMs);
        if (!saved.empty())
            SetClipboard(saved);
    }

    DWORD WINAPI ReplaceThread(void* parameter)
    {
        Job* job = (Job*)parameter;
        AcquireSRWLockExclusive(&g_sendLock);
        Replace(*job);
        ReleaseSRWLockExclusive(&g_sendLock);
        delete job;
        return 0;
    }
}

void TextSender::Initialize()
{
    g_clipboardOwner = CreateWindowExW(0, L"STATIC", L"JTools clipboard", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr,
                                       GetModuleHandleW(nullptr), nullptr);
}

void TextSender::ReplaceAsync(int backspaces, std::wstring expansion, WORD trailingVk)
{
    Job* job = new Job{ backspaces, std::move(expansion), trailingVk };
    HANDLE thread = CreateThread(nullptr, 0, ReplaceThread, job, 0, nullptr);
    if (thread)
        CloseHandle(thread);
    else
        delete job;
}
