#include "LogView.h"

#include "../Common/Theme.h"
#include "../Common/Util.h"
#include "imgui_internal.h"

#include <cmath>

namespace
{
    /// Decodes one UTF-8 character; returns its length in bytes.
    int NextChar(std::string_view text, size_t at, unsigned int& codepoint)
    {
        return ImTextCharFromUtf8(&codepoint, text.data() + at, text.data() + text.size());
    }

    bool IsWordChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_'
            || (unsigned char)c >= 0x80;
    }
}

LogView::LogView(std::string text)
{
    Append(text);
}

void LogView::Append(std::string_view text)
{
    size_t firstChangedLine = lineStarts_.size() - 1;
    size_t oldSize = text_.size();
    text_.append(text);

    // Lines end at \n, \r\n or a lone \r. A \r at the end of one chunk may be followed by the
    // \n at the start of the next, so a tentative line start after a \r moves past the \n.
    for (size_t i = oldSize; i < text_.size(); i++)
    {
        char c = text_[i];
        if (c == '\n')
        {
            if (i > 0 && text_[i - 1] == '\r' && lineStarts_.back() == i)
                lineStarts_.back() = i + 1;
            else
                lineStarts_.push_back(i + 1);
        }
        else if (c == '\r')
            lineStarts_.push_back(i + 1);
    }
    UpdateMaxColumns(firstChangedLine);
}

size_t LogView::DisplayLineCount() const
{
    return lineStarts_.size() - (lineStarts_.back() == text_.size() ? 1 : 0);
}

std::string_view LogView::Line(size_t index) const
{
    size_t start = lineStarts_[index];
    size_t end = index + 1 < lineStarts_.size() ? lineStarts_[index + 1] : text_.size();
    if (end > start && text_[end - 1] == '\n')
        end--;
    if (end > start && text_[end - 1] == '\r')
        end--;
    return std::string_view(text_).substr(start, end - start);
}

void LogView::UpdateMaxColumns(size_t fromLine)
{
    for (size_t i = fromLine; i < lineStarts_.size(); i++)
    {
        std::string_view line = Line(i);
        size_t columns = 0;
        for (char c : line)
        {
            if (c == '\t')
                columns += TabSize - columns % TabSize;
            else if (((unsigned char)c & 0xC0) != 0x80)
                columns++;
        }
        if (columns > maxColumns_)
            maxColumns_ = columns;
    }
}

size_t LogView::OffsetAtX(std::string_view line, float x) const
{
    ImFontBaked* font = ImGui::GetFontBaked();
    float spaceWidth = font->GetCharAdvance(' ');
    float at = 0;
    size_t column = 0;
    for (size_t i = 0; i < line.size();)
    {
        unsigned int c;
        int length = NextChar(line, i, c);
        float advance;
        if (c == '\t')
        {
            int spaces = TabSize - (int)(column % TabSize);
            advance = spaces * spaceWidth;
            column += spaces;
        }
        else
        {
            advance = font->GetCharAdvance((ImWchar)c);
            column++;
        }
        if (at + advance * 0.5f > x)
            return i;
        at += advance;
        i += length;
    }
    return line.size();
}

float LogView::XAtOffset(std::string_view line, size_t offset) const
{
    ImFontBaked* font = ImGui::GetFontBaked();
    float spaceWidth = font->GetCharAdvance(' ');
    float x = 0;
    size_t column = 0;
    for (size_t i = 0; i < line.size() && i < offset;)
    {
        unsigned int c;
        int length = NextChar(line, i, c);
        if (c == '\t')
        {
            int spaces = TabSize - (int)(column % TabSize);
            x += spaces * spaceWidth;
            column += spaces;
        }
        else
        {
            x += font->GetCharAdvance((ImWchar)c);
            column++;
        }
        i += length;
    }
    return x;
}

LogView::Position LogView::HitTest(ImVec2 point, ImVec2 origin, float lineHeight) const
{
    float row = std::floor((point.y - origin.y) / lineHeight);
    size_t line = row < 0 ? 0 : (size_t)row;
    if (line >= lineStarts_.size())
        return { lineStarts_.size() - 1, Line(lineStarts_.size() - 1).size() };
    return { line, OffsetAtX(Line(line), point.x - origin.x) };
}

void LogView::SelectWordAt(Position position)
{
    std::string_view line = Line(position.line);
    size_t start = position.offset, end = position.offset;
    while (start > 0 && IsWordChar(line[start - 1]))
        start--;
    while (end < line.size() && IsWordChar(line[end]))
        end++;
    anchor_ = { position.line, start };
    caret_ = { position.line, end };
}

void LogView::SelectAll()
{
    anchor_ = {};
    caret_ = { lineStarts_.size() - 1, Line(lineStarts_.size() - 1).size() };
}

void LogView::CopySelection() const
{
    if (!HasSelection())
        return;
    Position start = anchor_ < caret_ ? anchor_ : caret_;
    Position end = anchor_ < caret_ ? caret_ : anchor_;
    size_t from = lineStarts_[start.line] + start.offset;
    size_t to = lineStarts_[end.line] + end.offset;
    ImGui::SetClipboardText(text_.substr(from, to - from).c_str());
}

void LogView::Draw(const char* id, ImVec2 size, float fontSize, Color background, Color text,
                   const std::vector<HighlightRule>& rules)
{
    ImGui::PushStyleColor(ImGuiCol_ChildBg, background.ToU32());
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_ChildRounding, 0);
    ImGui::BeginChild(id, size, ImGuiChildFlags_None, ImGuiWindowFlags_HorizontalScrollbar | ImGuiWindowFlags_NoNav);
    ImGui::PopStyleVar(2);
    ImGui::PopStyleColor();

    ImGui::PushFont(g_fonts.mono, Theme::MonoEm(fontSize));
    const float lineHeight = std::round(ImMax(ImGui::GetFontSize(), S(fontSize) * 1.3f));
    const float spaceWidth = ImGui::GetFontBaked()->GetCharAdvance(' ');
    const ImVec2 padding = S(12, 8);
    const size_t lineCount = lineStarts_.size();

    ImVec2 viewSize = ImGui::GetContentRegionAvail();
    ImVec2 contentSize(ImMax(viewSize.x, padding.x * 2 + maxColumns_ * spaceWidth + spaceWidth),
                       ImMax(viewSize.y, padding.y * 2 + lineCount * lineHeight));

    ImVec2 contentMin = ImGui::GetCursorScreenPos();
    ImVec2 origin(contentMin.x + padding.x, contentMin.y + padding.y);

    // One invisible item covers the whole document: it sets the scrollable size and takes mouse input.
    ImGui::InvisibleButton("##text", contentSize, ImGuiButtonFlags_MouseButtonLeft | ImGuiButtonFlags_MouseButtonRight);
    ImVec2 mouse = ImGui::GetMousePos();
    if (ImGui::IsItemHovered())
        ImGui::SetMouseCursor(ImGuiMouseCursor_TextInput);

    if (ImGui::IsItemActivated() && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
    {
        Position hit = HitTest(mouse, origin, lineHeight);
        if (ImGui::GetMouseClickedCount(ImGuiMouseButton_Left) == 2)
            SelectWordAt(hit);
        else if (ImGui::GetIO().KeyShift)
            caret_ = hit;
        else
            anchor_ = caret_ = hit;
    }
    else if (ImGui::IsItemActive() && ImGui::IsMouseDragging(ImGuiMouseButton_Left, 0)
             && ImGui::GetMouseClickedCount(ImGuiMouseButton_Left) < 2)
    {
        caret_ = HitTest(mouse, origin, lineHeight);

        // Scroll while dragging past an edge.
        ImVec2 windowMin = ImGui::GetWindowPos(), windowSize = ImGui::GetWindowSize();
        if (mouse.y < windowMin.y)
            ImGui::SetScrollY(ImGui::GetScrollY() - lineHeight);
        else if (mouse.y > windowMin.y + windowSize.y)
            ImGui::SetScrollY(ImGui::GetScrollY() + lineHeight);
    }

    if (ImGui::BeginPopupContextItem("##logMenu"))
    {
        if (ImGui::MenuItem("Copy", "Ctrl+C", false, HasSelection()))
            CopySelection();
        if (ImGui::MenuItem("Select All", "Ctrl+A"))
            SelectAll();
        ImGui::EndPopup();
    }
    if (ImGui::IsWindowFocused())
    {
        if (ImGui::Shortcut(ImGuiMod_Ctrl | ImGuiKey_C))
            CopySelection();
        if (ImGui::Shortcut(ImGuiMod_Ctrl | ImGuiKey_A))
            SelectAll();
    }

    // Draw only the visible lines.
    float scrollY = ImGui::GetScrollY();
    size_t first = scrollY > padding.y ? (size_t)((scrollY - padding.y) / lineHeight) : 0;
    size_t last = ImMin(lineCount, first + (size_t)(viewSize.y / lineHeight) + 2);

    Position selectionStart = anchor_ < caret_ ? anchor_ : caret_;
    Position selectionEnd = anchor_ < caret_ ? caret_ : anchor_;
    bool hasSelection = HasSelection();

    ImDrawList* drawList = ImGui::GetWindowDrawList();
    ImU32 textColor = text.ToU32();
    ImU32 selectionColor = ImGui::GetColorU32(ImGuiCol_TextSelectedBg);
    float textOffsetY = (lineHeight - ImGui::GetFontSize()) * 0.5f;

    for (size_t i = first; i < last; i++)
    {
        std::string_view line = Line(i);
        float y = origin.y + i * lineHeight;

        expandedLine_.clear();
        size_t column = 0;
        for (char c : line)
        {
            if (c == '\t')
            {
                size_t spaces = TabSize - column % TabSize;
                expandedLine_.append(spaces, ' ');
                column += spaces;
            }
            else
            {
                expandedLine_ += c;
                if (((unsigned char)c & 0xC0) != 0x80)
                    column++;
            }
        }
        const char* begin = expandedLine_.data();
        const char* end = begin + expandedLine_.size();
        float width = ImGui::CalcTextSize(begin, end).x;

        for (const HighlightRule& rule : rules)
        {
            if (!rule.word.empty() && ContainsIgnoreCase(line, rule.word))
            {
                drawList->AddRectFilled(ImVec2(origin.x, y), ImVec2(origin.x + width, y + lineHeight), rule.color.ToU32());
                break;
            }
        }

        if (hasSelection && i >= selectionStart.line && i <= selectionEnd.line)
        {
            float from = i == selectionStart.line ? XAtOffset(line, selectionStart.offset) : 0;
            float to = i == selectionEnd.line ? XAtOffset(line, selectionEnd.offset) : width + spaceWidth;
            if (to > from)
                drawList->AddRectFilled(ImVec2(origin.x + from, y), ImVec2(origin.x + to, y + lineHeight), selectionColor);
        }

        drawList->AddText(ImVec2(origin.x, y + textOffsetY), textColor, begin, end);
    }

    if (scrollToEndFrames_ > 0)
    {
        ImGui::SetScrollY(contentSize.y);
        scrollToEndFrames_--;
    }

    ImGui::PopFont();
    ImGui::EndChild();
}
