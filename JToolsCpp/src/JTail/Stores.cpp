#include "Stores.h"

#include "../Common/Json.h"
#include "../Common/Util.h"

namespace
{
    bool LoadJson(const wchar_t* file, Json& json)
    {
        std::string text;
        return ReadTextFile(AppDataFile(L"JTail", file), text) && Json::Parse(text, json);
    }

    void SaveJson(const wchar_t* file, const Json& json)
    {
        // Written compactly, like the C# app's default JsonSerializer options.
        WriteTextFile(AppDataFile(L"JTail", file), json.Dump(false));
    }
}

HighlightRulesStore HighlightRulesStore::Load()
{
    HighlightRulesStore store;
    Json json;
    if (!LoadJson(L"highlights.json", json))
        return store;

    for (const Json& item : json.Items())
    {
        const Json* word = item.Get("Word");
        const Json* colorHex = item.Get("ColorHex");
        HighlightRule rule;
        if (word && colorHex && Color::Parse(colorHex->AsString(), rule.color))
        {
            rule.word = word->AsString();
            store.rules.push_back(rule);
        }
    }
    return store;
}

void HighlightRulesStore::Save() const
{
    Json json = Json::Array();
    for (const HighlightRule& rule : rules)
    {
        Json item = Json::Object();
        item.Set("Word", Json::String(rule.word));
        item.Set("ColorHex", Json::String(rule.color.ToString()));
        json.Push(std::move(item));
    }
    SaveJson(L"highlights.json", json);
}

// Before the app had light/dark themes, the Matrix colors were the saved defaults. Treat them as
// "not customized" in files from before CurrentVersion, so those users' log panes follow the theme too.
static constexpr int CurrentThemeVersion = 2;

LogThemeStore LogThemeStore::Load()
{
    LogThemeStore store;
    Json json;
    if (!LoadJson(L"theme.json", json) || json.GetType() != Json::Type::Object)
        return store;

    double fontSize = json.Get("FontSize") ? json.Get("FontSize")->AsNumber() : 0;
    store.fontSize = fontSize >= MinFontSize && fontSize <= MaxFontSize ? (float)fontSize : DefaultFontSize;

    bool isLegacy = !json.Get("Version") || json.Get("Version")->AsNumber() < CurrentThemeVersion;
    auto parseCustom = [&](const char* name, Color legacyDefault) -> std::optional<Color> {
        const Json* value = json.Get(name);
        Color color;
        if (!value || !value->IsString() || !Color::Parse(value->AsString(), color))
            return std::nullopt;
        if (isLegacy && color == legacyDefault)
            return std::nullopt;
        return color;
    };
    store.background = parseCustom("Background", MatrixBackground);
    store.text = parseCustom("Text", MatrixText);
    return store;
}

void LogThemeStore::Save() const
{
    Json json = Json::Object();
    json.Set("Background", background ? Json::String(background->ToString()) : Json());
    json.Set("Text", text ? Json::String(text->ToString()) : Json());
    json.Set("FontSize", Json::Number(fontSize));
    json.Set("Version", Json::Number(CurrentThemeVersion));
    SaveJson(L"theme.json", json);
}

void LogThemeStore::ResetToDefault()
{
    background.reset();
    text.reset();
    fontSize = DefaultFontSize;
    Save();
}

RecentFilesStore RecentFilesStore::Load()
{
    RecentFilesStore store;
    Json json;
    if (LoadJson(L"recent.json", json))
        for (const Json& item : json.Items())
            if (item.IsString())
                store.paths.push_back(item.AsString());
    return store;
}

void RecentFilesStore::Add(const std::string& path)
{
    std::erase_if(paths, [&](const std::string& p) { return EqualsIgnoreCase(p, path); });
    paths.insert(paths.begin(), path);
    if (paths.size() > MaxEntries)
        paths.resize(MaxEntries);
    Save();
}

void RecentFilesStore::Remove(const std::string& path)
{
    std::erase_if(paths, [&](const std::string& p) { return EqualsIgnoreCase(p, path); });
    Save();
}

void RecentFilesStore::Save() const
{
    Json json = Json::Array();
    for (const std::string& path : paths)
        json.Push(Json::String(path));
    SaveJson(L"recent.json", json);
}
