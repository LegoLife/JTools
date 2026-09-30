#pragma once

#include <cstdint>
#include <memory>
#include <string>

/// Reads a text file and reports content appended after that point.
/// Opens the file only for the duration of each read so it never holds a lock
/// between polls, letting other processes write to it freely.
class LogTailer
{
public:
    /// Opens the file, reads its entire contents, and records the end-of-file position so
    /// subsequent calls to ReadAppended pick up from there. Returns null if the file can't be read.
    static std::unique_ptr<LogTailer> Open(const std::wstring& path, std::string& initialText);

    /// Reads any bytes appended to the file since the last read. Returns false if the file
    /// was truncated (shorter than before), couldn't be opened, or nothing new was appended.
    bool ReadAppended(std::string& text);

    uint64_t Length() const { return position_; }

private:
    LogTailer(std::wstring path, uint64_t position) : path_(std::move(path)), position_(position) {}

    std::wstring path_;
    uint64_t position_;
};
