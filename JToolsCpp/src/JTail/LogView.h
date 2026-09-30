#pragma once

#include "Stores.h"

#include "imgui.h"

#include <string>
#include <string_view>
#include <vector>

/// A read-only, virtualized view of a (possibly huge) log: only the visible lines are drawn each
/// frame. Colors the background of any line matching a highlight rule (case-insensitive substring
/// match; the first matching rule in list order wins), and supports mouse selection and copying.
class LogView
{
public:
    explicit LogView(std::string text);

    void Append(std::string_view text);

    /// Lines in the file, not counting the empty line after a trailing newline.
    size_t DisplayLineCount() const;

    /// Scrolls to the last line on the next frames.
    void ScrollToEnd() { scrollToEndFrames_ = 2; }

    void Draw(const char* id, ImVec2 size, float fontSize, Color background, Color text,
              const std::vector<HighlightRule>& rules);

private:
    struct Position
    {
        size_t line = 0;
        size_t offset = 0; // byte offset within the line

        bool operator<(const Position& o) const { return line != o.line ? line < o.line : offset < o.offset; }
        bool operator==(const Position& o) const { return line == o.line && offset == o.offset; }
    };

    std::string_view Line(size_t index) const;
    void UpdateMaxColumns(size_t fromLine);
    Position HitTest(ImVec2 point, ImVec2 origin, float lineHeight) const;
    size_t OffsetAtX(std::string_view line, float x) const;
    float XAtOffset(std::string_view line, size_t offset) const;
    void SelectWordAt(Position position);
    void SelectAll();
    void CopySelection() const;
    bool HasSelection() const { return !(anchor_ == caret_); }

    static constexpr int TabSize = 4;

    std::string text_;
    std::vector<size_t> lineStarts_{ 0 };
    size_t maxColumns_ = 0;

    Position anchor_, caret_;
    int scrollToEndFrames_ = 0;
    std::string expandedLine_; // scratch buffer for drawing a line with tabs expanded
};
