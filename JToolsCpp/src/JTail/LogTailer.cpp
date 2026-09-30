#include "LogTailer.h"

#include <windows.h>

namespace
{
    HANDLE OpenShared(const std::wstring& path)
    {
        return CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    }

    /// Reads from the file's current position until `size` bytes are read or the file ends.
    bool ReadBytes(HANDLE file, std::string& buffer, uint64_t size)
    {
        buffer.resize((size_t)size);
        size_t total = 0;
        while (total < buffer.size())
        {
            DWORD chunk = (DWORD)((buffer.size() - total) > 0x10000000 ? 0x10000000 : buffer.size() - total);
            DWORD read = 0;
            if (!ReadFile(file, buffer.data() + total, chunk, &read, nullptr))
                return false;
            if (read == 0)
                break;
            total += read;
        }
        buffer.resize(total);
        return true;
    }
}

std::unique_ptr<LogTailer> LogTailer::Open(const std::wstring& path, std::string& initialText)
{
    HANDLE file = OpenShared(path);
    if (file == INVALID_HANDLE_VALUE)
        return nullptr;

    LARGE_INTEGER length{};
    bool ok = GetFileSizeEx(file, &length) && ReadBytes(file, initialText, (uint64_t)length.QuadPart);
    CloseHandle(file);
    if (!ok)
        return nullptr;

    // Skip a UTF-8 byte order mark so it doesn't show up as a stray character.
    if (initialText.starts_with("\xEF\xBB\xBF"))
        initialText.erase(0, 3);

    return std::unique_ptr<LogTailer>(new LogTailer(path, (uint64_t)length.QuadPart));
}

bool LogTailer::ReadAppended(std::string& text)
{
    HANDLE file = OpenShared(path_);
    if (file == INVALID_HANDLE_VALUE)
        return false;

    bool appended = false;
    LARGE_INTEGER length{};
    if (GetFileSizeEx(file, &length))
    {
        auto size = (uint64_t)length.QuadPart;
        if (size < position_)
        {
            // File was truncated or replaced; out of scope to recover, just resync to current end.
            position_ = size;
        }
        else if (size > position_)
        {
            LARGE_INTEGER offset;
            offset.QuadPart = (LONGLONG)position_;
            if (SetFilePointerEx(file, offset, nullptr, FILE_BEGIN) && ReadBytes(file, text, size - position_))
            {
                position_ += text.size();
                appended = !text.empty();
            }
        }
    }
    CloseHandle(file);
    return appended;
}
