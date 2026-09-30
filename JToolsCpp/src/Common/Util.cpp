#include "Util.h"

#include <windows.h>
#include <shlobj.h>

#include <cstdio>

std::wstring Widen(std::string_view utf8)
{
    if (utf8.empty())
        return {};
    int length = MultiByteToWideChar(CP_UTF8, 0, utf8.data(), (int)utf8.size(), nullptr, 0);
    std::wstring result(length, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, utf8.data(), (int)utf8.size(), result.data(), length);
    return result;
}

std::string Narrow(std::wstring_view wide)
{
    if (wide.empty())
        return {};
    int length = WideCharToMultiByte(CP_UTF8, 0, wide.data(), (int)wide.size(), nullptr, 0, nullptr, nullptr);
    std::string result(length, '\0');
    WideCharToMultiByte(CP_UTF8, 0, wide.data(), (int)wide.size(), result.data(), length, nullptr, nullptr);
    return result;
}

std::wstring AppDataFile(const wchar_t* folder, const wchar_t* file)
{
    std::wstring result;
    PWSTR appData = nullptr;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_RoamingAppData, 0, nullptr, &appData)))
        result = appData;
    CoTaskMemFree(appData);
    return result + L'\\' + folder + L'\\' + file;
}

bool ReadTextFile(const std::wstring& path, std::string& out)
{
    HANDLE file = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                              nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return false;

    LARGE_INTEGER size{};
    bool ok = GetFileSizeEx(file, &size) && size.QuadPart < 0x7FFFFFFF;
    if (ok)
    {
        out.resize((size_t)size.QuadPart);
        DWORD read = 0;
        ok = out.empty() || (ReadFile(file, out.data(), (DWORD)out.size(), &read, nullptr) && read == out.size());
    }
    CloseHandle(file);
    return ok;
}

bool WriteTextFile(const std::wstring& path, std::string_view text)
{
    auto slash = path.find_last_of(L'\\');
    if (slash != std::wstring::npos)
        CreateDirectoryW(path.substr(0, slash).c_str(), nullptr); // fails harmlessly if it exists

    std::wstring tempPath = path + L".tmp";
    HANDLE file = CreateFileW(tempPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return false;

    DWORD written = 0;
    bool ok = WriteFile(file, text.data(), (DWORD)text.size(), &written, nullptr) && written == text.size();
    CloseHandle(file);
    return ok && MoveFileExW(tempPath.c_str(), path.c_str(), MOVEFILE_REPLACE_EXISTING);
}

bool FileExists(const std::wstring& path)
{
    DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_DIRECTORY);
}

static char FoldAscii(char c)
{
    return c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;
}

bool ContainsIgnoreCase(std::string_view haystack, std::string_view needle)
{
    if (needle.empty())
        return true;
    if (needle.size() > haystack.size())
        return false;

    char first = FoldAscii(needle[0]);
    for (size_t i = 0, last = haystack.size() - needle.size(); i <= last; i++)
    {
        if (FoldAscii(haystack[i]) != first)
            continue;
        size_t j = 1;
        while (j < needle.size() && FoldAscii(haystack[i + j]) == FoldAscii(needle[j]))
            j++;
        if (j == needle.size())
            return true;
    }
    return false;
}

int CompareIgnoreCase(std::string_view a, std::string_view b)
{
    size_t n = a.size() < b.size() ? a.size() : b.size();
    for (size_t i = 0; i < n; i++)
    {
        auto ca = (unsigned char)FoldAscii(a[i]);
        auto cb = (unsigned char)FoldAscii(b[i]);
        if (ca != cb)
            return ca < cb ? -1 : 1;
    }
    return a.size() == b.size() ? 0 : (a.size() < b.size() ? -1 : 1);
}

bool EqualsIgnoreCase(std::string_view a, std::string_view b)
{
    return a.size() == b.size() && CompareIgnoreCase(a, b) == 0;
}

std::string Trim(std::string_view s)
{
    auto isSpace = [](char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n'; };
    size_t start = 0, end = s.size();
    while (start < end && isSpace(s[start]))
        start++;
    while (end > start && isSpace(s[end - 1]))
        end--;
    return std::string(s.substr(start, end - start));
}

std::string ReplaceLineEndings(std::string_view s, std::string_view newline)
{
    std::string result;
    result.reserve(s.size());
    for (size_t i = 0; i < s.size(); i++)
    {
        if (s[i] == '\r')
        {
            if (i + 1 < s.size() && s[i + 1] == '\n')
                i++;
            result += newline;
        }
        else if (s[i] == '\n')
            result += newline;
        else
            result += s[i];
    }
    return result;
}

std::string FormatThousands(uint64_t n)
{
    std::string digits = std::to_string(n);
    std::string result;
    for (size_t i = 0; i < digits.size(); i++)
    {
        if (i > 0 && (digits.size() - i) % 3 == 0)
            result += ',';
        result += digits[i];
    }
    return result;
}

std::string FormatFileSize(uint64_t bytes)
{
    static const char* units[] = { "B", "KB", "MB", "GB" };
    double size = (double)bytes;
    int unit = 0;
    while (size >= 1024 && unit < 3)
    {
        size /= 1024;
        unit++;
    }

    char buffer[32];
    double rounded = (double)(int64_t)(size * 10 + 0.5) / 10;
    if (unit == 0 || rounded == (double)(int64_t)rounded)
        snprintf(buffer, sizeof buffer, "%.0f %s", unit == 0 ? size : rounded, units[unit]);
    else
        snprintf(buffer, sizeof buffer, "%.1f %s", rounded, units[unit]);
    return buffer;
}

std::string FileNameOf(std::string_view path)
{
    auto slash = path.find_last_of("\\/");
    return std::string(slash == std::string_view::npos ? path : path.substr(slash + 1));
}

uint64_t NowMs()
{
    return GetTickCount64();
}
