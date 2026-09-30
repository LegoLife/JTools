#pragma once

#include <cstdint>
#include <string>
#include <string_view>

std::wstring Widen(std::string_view utf8);
std::string Narrow(std::wstring_view wide);

/// %APPDATA%\<folder>\<file>, e.g. AppDataFile(L"JTail", L"recent.json").
std::wstring AppDataFile(const wchar_t* folder, const wchar_t* file);

bool ReadTextFile(const std::wstring& path, std::string& out);

/// Writes through a temp file and a rename, creating the parent folder if needed.
bool WriteTextFile(const std::wstring& path, std::string_view text);

bool FileExists(const std::wstring& path);

// Case-insensitive comparisons fold ASCII letters only, which covers log levels, paths and abbreviations.
bool ContainsIgnoreCase(std::string_view haystack, std::string_view needle);
int CompareIgnoreCase(std::string_view a, std::string_view b);
bool EqualsIgnoreCase(std::string_view a, std::string_view b);

std::string Trim(std::string_view s);
std::string ReplaceLineEndings(std::string_view s, std::string_view newline);

/// e.g. 12,345
std::string FormatThousands(uint64_t n);

/// e.g. "12.4 KB"
std::string FormatFileSize(uint64_t bytes);

std::string FileNameOf(std::string_view path);

uint64_t NowMs();
