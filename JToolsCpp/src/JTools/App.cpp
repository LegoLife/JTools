#include "App.h"

#include "../Common/Json.h"
#include "../Common/Theme.h"
#include "../Common/Util.h"
#include "../Common/Widgets.h"

using namespace Widgets;

static std::wstring SettingsPath()
{
    return AppDataFile(L"JTools", L"settings.json");
}

JToolsSettings JToolsSettings::Load()
{
    JToolsSettings settings;
    std::string text;
    Json json;
    if (ReadTextFile(SettingsPath(), text) && Json::Parse(text, json))
    {
        if (const Json* theme = json.Get("Theme"))
        {
            const std::string& name = theme->AsString();
            double number = theme->AsNumber(-1);
            settings.theme = name == "Light" || number == 1 ? AppTheme::Light
                           : name == "System" || number == 2 ? AppTheme::System
                                                             : AppTheme::Dark;
        }
    }
    return settings;
}

void JToolsSettings::Save() const
{
    Json json = Json::Object();
    json.Set("Theme", Json::String(theme == AppTheme::Light ? "Light" : theme == AppTheme::System ? "System" : "Dark"));
    WriteTextFile(SettingsPath(), json.Dump());
}

App::App() = default;

bool App::UsesDarkTheme() const
{
    switch (settings_.theme)
    {
    case AppTheme::Light: return false;
    case AppTheme::System: return !Theme::WindowsUsesLightTheme();
    default: return true;
    }
}

std::string App::WindowTitle() const
{
    return std::string("JTools — ") + (tool_ == Tool::Tail ? tail_.Title() : "Text Expander");
}

void App::Draw()
{
    ImGuiViewport* viewport = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(viewport->WorkPos);
    ImGui::SetNextWindowSize(viewport->WorkSize);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);
    ImGui::Begin("##root", nullptr,
                 ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoSavedSettings
                     | ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleVar(3);

    const float sideBarWidth = S(SideBarWidth);
    ImVec2 size = ImGui::GetContentRegionAvail();
    ImVec2 origin = ImGui::GetCursorScreenPos();
    DrawSideBar(size.y);

    ImGui::SetCursorScreenPos(ImVec2(origin.x + sideBarWidth, origin.y));
    ImGui::BeginChild("##content", ImVec2(size.x - sideBarWidth, size.y), ImGuiChildFlags_None,
                      ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
    ImVec2 contentSize = ImGui::GetContentRegionAvail();
    if (tool_ == Tool::Tail)
        tail_.Draw(contentSize);
    else
        expanderView_.Draw(contentSize);
    ImGui::EndChild();

    ImGui::End();
}

void App::DrawSideBar(float height)
{
    const Palette& p = Theme::Current();
    const float width = S(SideBarWidth), margin = S(6);
    ImVec2 origin = ImGui::GetCursorScreenPos();
    ImDrawList* drawList = ImGui::GetWindowDrawList();
    drawList->AddRectFilled(origin, ImVec2(origin.x + width, origin.y + height), p.chrome);
    drawList->AddLine(ImVec2(origin.x + width - 1, origin.y), ImVec2(origin.x + width - 1, origin.y + height), p.border);

    float buttonWidth = width - margin * 2;
    ImGui::SetCursorScreenPos(ImVec2(origin.x + margin, origin.y + margin));
    if (NavButton("##tail", Icon::Document, "Tail", tool_ == Tool::Tail, buttonWidth))
        tool_ = Tool::Tail;
    ImGui::SetItemTooltip("JTail: tail log files");

    ImGui::SetCursorScreenPos(ImVec2(origin.x + margin, ImGui::GetItemRectMax().y + S(4)));
    if (NavButton("##expand", Icon::Keyboard, "Expand", tool_ == Tool::Expander, buttonWidth))
        tool_ = Tool::Expander;
    ImGui::SetItemTooltip("Text Expander: abbreviations");

    ImGui::SetCursorScreenPos(ImVec2(origin.x + margin, origin.y + height - margin - S(NavButtonHeight)));
    if (NavButton("##theme", Icon::Theme, "Theme", false, buttonWidth))
        ImGui::OpenPopup("##themeMenu");
    ImGui::SetItemTooltip("Dark or light theme");

    ImGui::SetNextWindowPos(ImVec2(ImGui::GetItemRectMax().x + S(4), ImGui::GetItemRectMax().y), ImGuiCond_Always,
                            ImVec2(0, 1));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, S(6, 6));
    if (ImGui::BeginPopup("##themeMenu"))
    {
        AppTheme chosen = settings_.theme;
        if (ImGui::MenuItem("Dark", nullptr, chosen == AppTheme::Dark))
            chosen = AppTheme::Dark;
        if (ImGui::MenuItem("Light", nullptr, chosen == AppTheme::Light))
            chosen = AppTheme::Light;
        if (ImGui::MenuItem("Use Windows setting", nullptr, chosen == AppTheme::System))
            chosen = AppTheme::System;
        ImGui::EndPopup();

        if (chosen != settings_.theme)
        {
            settings_.theme = chosen;
            settings_.Save();
        }
    }
    ImGui::PopStyleVar();
}
