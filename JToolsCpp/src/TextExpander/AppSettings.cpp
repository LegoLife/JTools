#include "AppSettings.h"

#include "../Common/Json.h"
#include "../Common/Util.h"

#include <vector>

static std::wstring SettingsPath()
{
    return AppDataFile(L"TextExpander", L"settings.json");
}

AppSettings AppSettings::Load()
{
    AppSettings settings;
    std::string text;
    Json json;
    if (!ReadTextFile(SettingsPath(), text) || !Json::Parse(text, json))
        return settings;

    if (const Json* mode = json.Get("Mode"))
    {
        // Written as a name by the C# app's JsonStringEnumConverter, but a number is accepted too.
        if (mode->IsString())
            settings.mode = mode->AsString() == "Immediate" ? ExpandMode::Immediate : ExpandMode::TriggerKey;
        else if (mode->IsNumber())
            settings.mode = mode->AsNumber() == 0 ? ExpandMode::Immediate : ExpandMode::TriggerKey;
    }
    auto readBool = [&](const char* name, bool& target) {
        if (const Json* value = json.Get(name))
            target = value->AsBool(target);
    };
    readBool("TriggerOnSpace", settings.triggerOnSpace);
    readBool("TriggerOnTab", settings.triggerOnTab);
    readBool("TriggerOnEnter", settings.triggerOnEnter);
    readBool("KeepTriggerKey", settings.keepTriggerKey);
    return settings;
}

void AppSettings::Save() const
{
    Json json = Json::Object();
    json.Set("Mode", Json::String(mode == ExpandMode::Immediate ? "Immediate" : "TriggerKey"));
    json.Set("TriggerOnSpace", Json::Bool(triggerOnSpace));
    json.Set("TriggerOnTab", Json::Bool(triggerOnTab));
    json.Set("TriggerOnEnter", Json::Bool(triggerOnEnter));
    json.Set("KeepTriggerKey", Json::Bool(keepTriggerKey));
    WriteTextFile(SettingsPath(), json.Dump());
}

bool AppSettings::IsTriggerKey(int vk) const
{
    switch (vk)
    {
    case VK_SPACE: return triggerOnSpace;
    case VK_TAB: return triggerOnTab;
    case VK_RETURN: return triggerOnEnter;
    default: return false;
    }
}

std::string AppSettings::DescribeTriggerKeys() const
{
    std::vector<const char*> keys;
    if (triggerOnSpace) keys.push_back("Space");
    if (triggerOnTab) keys.push_back("Tab");
    if (triggerOnEnter) keys.push_back("Enter");

    if (keys.empty())
        return "(no key selected)";

    std::string result = keys[0];
    for (size_t i = 1; i < keys.size(); i++)
        result += std::string(i + 1 == keys.size() ? " or " : ", ") + keys[i];
    return result;
}

AppSettings SharedSettings::Get() const
{
    AcquireSRWLockShared(&lock_);
    AppSettings copy = value_;
    ReleaseSRWLockShared(&lock_);
    return copy;
}

void SharedSettings::Set(const AppSettings& settings)
{
    AcquireSRWLockExclusive(&lock_);
    value_ = settings;
    ReleaseSRWLockExclusive(&lock_);
    settings.Save();
}

void SharedSettings::Load()
{
    AppSettings loaded = AppSettings::Load();
    AcquireSRWLockExclusive(&lock_);
    value_ = loaded;
    ReleaseSRWLockExclusive(&lock_);
}
