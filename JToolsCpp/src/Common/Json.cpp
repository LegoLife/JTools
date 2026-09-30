#include "Json.h"

#include "Util.h"

#include <cstdio>
#include <cstdlib>

Json Json::Bool(bool value)
{
    Json json;
    json.type_ = Type::Bool;
    json.bool_ = value;
    return json;
}

Json Json::Number(double value)
{
    Json json;
    json.type_ = Type::Number;
    json.number_ = value;
    return json;
}

Json Json::String(std::string value)
{
    Json json;
    json.type_ = Type::String;
    json.string_ = std::move(value);
    return json;
}

Json Json::Array()
{
    Json json;
    json.type_ = Type::Array;
    return json;
}

Json Json::Object()
{
    Json json;
    json.type_ = Type::Object;
    return json;
}

const Json* Json::Get(std::string_view key) const
{
    for (auto& [name, value] : members_)
        if (EqualsIgnoreCase(name, key))
            return &value;
    return nullptr;
}

Json& Json::Push(Json value)
{
    items_.push_back(std::move(value));
    return *this;
}

Json& Json::Set(std::string key, Json value)
{
    members_.emplace_back(std::move(key), std::move(value));
    return *this;
}

namespace
{
    struct Parser
    {
        std::string_view text;
        size_t pos = 0;

        void SkipWhitespace()
        {
            while (pos < text.size() && (text[pos] == ' ' || text[pos] == '\t' || text[pos] == '\r' || text[pos] == '\n'))
                pos++;
        }

        bool Consume(char c)
        {
            SkipWhitespace();
            if (pos < text.size() && text[pos] == c)
            {
                pos++;
                return true;
            }
            return false;
        }

        bool Literal(std::string_view word)
        {
            if (text.substr(pos, word.size()) != word)
                return false;
            pos += word.size();
            return true;
        }

        static void AppendUtf8(std::string& out, unsigned codepoint)
        {
            if (codepoint < 0x80)
                out += (char)codepoint;
            else if (codepoint < 0x800)
            {
                out += (char)(0xC0 | (codepoint >> 6));
                out += (char)(0x80 | (codepoint & 0x3F));
            }
            else if (codepoint < 0x10000)
            {
                out += (char)(0xE0 | (codepoint >> 12));
                out += (char)(0x80 | ((codepoint >> 6) & 0x3F));
                out += (char)(0x80 | (codepoint & 0x3F));
            }
            else
            {
                out += (char)(0xF0 | (codepoint >> 18));
                out += (char)(0x80 | ((codepoint >> 12) & 0x3F));
                out += (char)(0x80 | ((codepoint >> 6) & 0x3F));
                out += (char)(0x80 | (codepoint & 0x3F));
            }
        }

        bool Hex4(unsigned& value)
        {
            if (pos + 4 > text.size())
                return false;
            value = 0;
            for (int i = 0; i < 4; i++)
            {
                char c = text[pos++];
                value <<= 4;
                if (c >= '0' && c <= '9') value |= c - '0';
                else if (c >= 'a' && c <= 'f') value |= c - 'a' + 10;
                else if (c >= 'A' && c <= 'F') value |= c - 'A' + 10;
                else return false;
            }
            return true;
        }

        bool ParseString(std::string& out)
        {
            if (!Consume('"'))
                return false;
            while (pos < text.size())
            {
                char c = text[pos++];
                if (c == '"')
                    return true;
                if (c != '\\')
                {
                    out += c;
                    continue;
                }
                if (pos >= text.size())
                    return false;
                switch (text[pos++])
                {
                case '"': out += '"'; break;
                case '\\': out += '\\'; break;
                case '/': out += '/'; break;
                case 'b': out += '\b'; break;
                case 'f': out += '\f'; break;
                case 'n': out += '\n'; break;
                case 'r': out += '\r'; break;
                case 't': out += '\t'; break;
                case 'u':
                {
                    unsigned codepoint;
                    if (!Hex4(codepoint))
                        return false;
                    if (codepoint >= 0xD800 && codepoint < 0xDC00 && text.substr(pos, 2) == "\\u")
                    {
                        pos += 2;
                        unsigned low;
                        if (!Hex4(low))
                            return false;
                        codepoint = 0x10000 + ((codepoint - 0xD800) << 10) + (low - 0xDC00);
                    }
                    AppendUtf8(out, codepoint);
                    break;
                }
                default:
                    return false;
                }
            }
            return false;
        }

        bool ParseValue(Json& out, int depth)
        {
            if (depth > 64)
                return false;
            SkipWhitespace();
            if (pos >= text.size())
                return false;

            char c = text[pos];
            if (c == '{')
            {
                pos++;
                out = Json::Object();
                if (Consume('}'))
                    return true;
                do
                {
                    std::string key;
                    Json value;
                    if (!ParseString(key) || !Consume(':') || !ParseValue(value, depth + 1))
                        return false;
                    out.Set(std::move(key), std::move(value));
                } while (Consume(','));
                return Consume('}');
            }
            if (c == '[')
            {
                pos++;
                out = Json::Array();
                if (Consume(']'))
                    return true;
                do
                {
                    Json value;
                    if (!ParseValue(value, depth + 1))
                        return false;
                    out.Push(std::move(value));
                } while (Consume(','));
                return Consume(']');
            }
            if (c == '"')
            {
                std::string s;
                if (!ParseString(s))
                    return false;
                out = Json::String(std::move(s));
                return true;
            }
            if (Literal("true")) { out = Json::Bool(true); return true; }
            if (Literal("false")) { out = Json::Bool(false); return true; }
            if (Literal("null")) { out = Json(); return true; }

            std::string number(text.substr(pos, 64));
            char* end = nullptr;
            double value = strtod(number.c_str(), &end);
            if (end == number.c_str())
                return false;
            pos += end - number.c_str();
            out = Json::Number(value);
            return true;
        }
    };

    void AppendEscaped(std::string& out, const std::string& s)
    {
        out += '"';
        for (char c : s)
        {
            switch (c)
            {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if ((unsigned char)c < 0x20)
                {
                    char buffer[8];
                    snprintf(buffer, sizeof buffer, "\\u%04x", c);
                    out += buffer;
                }
                else
                    out += c;
            }
        }
        out += '"';
    }
}

bool Json::Parse(std::string_view text, Json& out)
{
    if (text.substr(0, 3) == "\xEF\xBB\xBF")
        text.remove_prefix(3);
    Parser parser{ text };
    if (!parser.ParseValue(out, 0))
        return false;
    parser.SkipWhitespace();
    return parser.pos == text.size();
}

std::string Json::Dump(bool indented) const
{
    std::string out;
    DumpTo(out, indented, 0);
    return out;
}

void Json::DumpTo(std::string& out, bool indented, int depth) const
{
    auto newline = [&](int level) {
        if (!indented)
            return;
        out += "\r\n";
        out.append(level * 2, ' ');
    };

    switch (type_)
    {
    case Type::Null: out += "null"; break;
    case Type::Bool: out += bool_ ? "true" : "false"; break;
    case Type::Number:
    {
        char buffer[32];
        snprintf(buffer, sizeof buffer, "%.15g", number_);
        out += buffer;
        break;
    }
    case Type::String: AppendEscaped(out, string_); break;
    case Type::Array:
        out += '[';
        for (size_t i = 0; i < items_.size(); i++)
        {
            if (i > 0)
                out += ',';
            newline(depth + 1);
            items_[i].DumpTo(out, indented, depth + 1);
        }
        if (!items_.empty())
            newline(depth);
        out += ']';
        break;
    case Type::Object:
        out += '{';
        for (size_t i = 0; i < members_.size(); i++)
        {
            if (i > 0)
                out += ',';
            newline(depth + 1);
            AppendEscaped(out, members_[i].first);
            out += indented ? ": " : ":";
            members_[i].second.DumpTo(out, indented, depth + 1);
        }
        if (!members_.empty())
            newline(depth);
        out += '}';
        break;
    }
}
