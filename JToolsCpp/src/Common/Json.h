#pragma once

#include <string>
#include <string_view>
#include <utility>
#include <vector>

/// A small JSON value with a parser and writer, enough for the settings files (which stay
/// compatible with the ones the C# JTools writes).
class Json
{
public:
    enum class Type { Null, Bool, Number, String, Array, Object };

    Json() = default;
    static Json Bool(bool value);
    static Json Number(double value);
    static Json String(std::string value);
    static Json Array();
    static Json Object();

    Type GetType() const { return type_; }
    bool IsNull() const { return type_ == Type::Null; }
    bool IsString() const { return type_ == Type::String; }
    bool IsNumber() const { return type_ == Type::Number; }

    bool AsBool(bool fallback = false) const { return type_ == Type::Bool ? bool_ : fallback; }
    double AsNumber(double fallback = 0) const { return type_ == Type::Number ? number_ : fallback; }
    const std::string& AsString() const { return string_; }

    /// Array elements (empty unless this is an array).
    const std::vector<Json>& Items() const { return items_; }

    /// Object member by name (ASCII case-insensitive), or null if missing.
    const Json* Get(std::string_view key) const;

    Json& Push(Json value);
    Json& Set(std::string key, Json value);

    static bool Parse(std::string_view text, Json& out);
    std::string Dump(bool indented = true) const;

private:
    void DumpTo(std::string& out, bool indented, int depth) const;

    Type type_ = Type::Null;
    bool bool_ = false;
    double number_ = 0;
    std::string string_;
    std::vector<Json> items_;
    std::vector<std::pair<std::string, Json>> members_;
};
