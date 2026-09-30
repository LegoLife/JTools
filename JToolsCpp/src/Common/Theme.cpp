#include "Theme.h"

#include "Util.h"

#include <windows.h>

#include <memory>
#include <string>
#include <vector>

Fonts g_fonts;

namespace
{
    constexpr ImU32 Rgb(uint32_t rgb) { return Color::FromRgb(rgb).ToU32(); }

    const Palette DarkPalette = {
        .chrome = Rgb(0x18191C), .background = Rgb(0x1E1F23), .card = Rgb(0x26282D),
        .surface = Rgb(0x2C2E33), .surfacePressed = Rgb(0x35383E), .border = Rgb(0x34373D),
        .text = Rgb(0xE6E7E9), .textDim = Rgb(0x9A9EA6),
        .accent = Rgb(0x3FB950), .accentSoft = Rgb(0x1F3526), .selection = Rgb(0x233A2A), .selectionHover = Rgb(0x2A4532),
        .inputBackground = Rgb(0x18191C), .inputHover = Rgb(0x1C1D21),
        .warningBackground = Rgb(0x2D2715), .warningBorder = Rgb(0x5E4E1B), .warningText = Rgb(0xE3B341),
        .accentFill = Rgb(0x238636), .accentFillHover = Rgb(0x2EA043), .accentFillPressed = Rgb(0x1A7F37),
        .logBackground = Color::FromRgb(0x141518), .logText = Color::FromRgb(0xD4D6DA),
    };

    const Palette LightPalette = {
        .chrome = Rgb(0xF0F1F3), .background = Rgb(0xF8F9FA), .card = Rgb(0xFFFFFF),
        .surface = Rgb(0xE6E8EB), .surfacePressed = Rgb(0xDADDE1), .border = Rgb(0xD7DADF),
        .text = Rgb(0x1F2328), .textDim = Rgb(0x5F6670),
        .accent = Rgb(0x1A7F37), .accentSoft = Rgb(0xDCF2E3), .selection = Rgb(0xDCF2E3), .selectionHover = Rgb(0xCBEBD5),
        .inputBackground = Rgb(0xFFFFFF), .inputHover = Rgb(0xFBFBFC),
        .warningBackground = Rgb(0xFFF8E1), .warningBorder = Rgb(0xE6C65C), .warningText = Rgb(0x7A5A00),
        .accentFill = Rgb(0x1A7F37), .accentFillHover = Rgb(0x238636), .accentFillPressed = Rgb(0x116329),
        .logBackground = Color::FromRgb(0xFFFFFF), .logText = Color::FromRgb(0x1F2328),
    };

    const Palette* g_palette = &DarkPalette;
    float g_dpiScale = 1.0f;

    ImVec4 V(ImU32 color) { return ImGui::ColorConvertU32ToFloat4(color); }

    ImVec4 WithAlpha(ImU32 color, float alpha)
    {
        ImVec4 v = V(color);
        v.w = alpha;
        return v;
    }

    std::string FontPath(const char* file)
    {
        char windows[MAX_PATH];
        GetWindowsDirectoryA(windows, MAX_PATH);
        return std::string(windows) + "\\Fonts\\" + file;
    }

    /// A font's line height (ascent + descent) in ems, from its 'hhea' and 'head' tables.
    float LinePerEm(const std::string& data)
    {
        if (data.size() < 12)
            return 1.0f;

        auto u16 = [&](size_t at) { return at + 2 <= data.size() ? (uint16_t)((uint8_t)data[at] << 8 | (uint8_t)data[at + 1]) : 0; };
        auto u32 = [&](size_t at) { return (uint32_t)u16(at) << 16 | u16(at + 2); };

        size_t head = 0, hhea = 0;
        for (size_t i = 0, count = u16(4); i < count; i++)
        {
            size_t record = 12 + 16 * i;
            std::string_view tag(data.data() + record, record + 4 <= data.size() ? 4 : 0);
            if (tag == "head") head = u32(record + 8);
            if (tag == "hhea") hhea = u32(record + 8);
        }
        float unitsPerEm = head ? u16(head + 18) : 0;
        float line = hhea ? (float)(int16_t)u16(hhea + 4) - (float)(int16_t)u16(hhea + 6) : 0;
        return unitsPerEm > 0 && line > 0 ? line / unitsPerEm : 1.0f;
    }

    struct FontFile
    {
        std::string path;
        std::string data;
        float linePerEm;
    };

    /// Font files, each read once and shared by every font that uses it (the atlas doesn't copy them).
    std::vector<std::unique_ptr<FontFile>> g_fontFiles;

    /// The first of the files that exists in the Windows fonts folder, or null.
    const FontFile* LoadFontFile(std::initializer_list<const char*> files)
    {
        for (const char* file : files)
        {
            std::string path = FontPath(file);
            for (const auto& loaded : g_fontFiles)
                if (loaded->path == path)
                    return loaded.get();

            std::string data;
            if (!ReadTextFile(Widen(path), data))
                continue;
            float linePerEm = LinePerEm(data);
            g_fontFiles.push_back(std::make_unique<FontFile>(FontFile{ path, std::move(data), linePerEm }));
            return g_fontFiles.back().get();
        }
        return nullptr;
    }

    float g_uiLinePerEm = 1.0f;
    float g_monoLinePerEm = 1.0f;

    /// Adds a font; a merged font is sized so its glyphs have the same em size as the main font's.
    ImFont* AddFont(const FontFile& file, float size, bool merge = false, float mainLinePerEm = 1.0f,
                    const ImWchar* excludeRanges = nullptr, float offsetY = 0)
    {
        ImFontConfig config;
        config.FontDataOwnedByAtlas = false;
        config.MergeMode = merge;
        config.GlyphExcludeRanges = excludeRanges;
        config.GlyphOffset.y = offsetY;
        if (merge)
            size = size * file.linePerEm / mainLinePerEm;
        return ImGui::GetIO().Fonts->AddFontFromMemoryTTF((void*)file.data.data(), (int)file.data.size(), size, &config);
    }

    /// Icons, then symbols such as ⏎ and ✓ that the text fonts lack.
    void MergeFallbacks(float size, float mainLinePerEm)
    {
        if (const FontFile* icons = LoadFontFile({ "SegoeIcons.ttf", "segmdl2.ttf" }))
            AddFont(*icons, size, true, mainLinePerEm, nullptr, size * 0.05f); // icons sit a little high
        if (const FontFile* symbols = LoadFontFile({ "seguisym.ttf" }))
            AddFont(*symbols, size, true, mainLinePerEm);
    }
}

float Theme::Em(float em)
{
    return em * g_uiLinePerEm;
}

float Theme::MonoEm(float em)
{
    return em * g_monoLinePerEm;
}

const Palette& Theme::Current()
{
    return *g_palette;
}

bool Theme::IsDark()
{
    return g_palette == &DarkPalette;
}

float Theme::DpiScale()
{
    return g_dpiScale;
}

bool Theme::WindowsUsesLightTheme()
{
    DWORD value = 0, size = sizeof value;
    LSTATUS status = RegGetValueW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                                  L"AppsUseLightTheme", RRF_RT_REG_DWORD, nullptr, &value, &size);
    return status == ERROR_SUCCESS && value != 0;
}

void Theme::LoadFonts()
{
    // Segoe UI has a few private-use glyphs of its own; keep the icon font's instead.
    static const ImWchar excludePrivateUse[] = { 0xE000, 0xF8FF, 0 };

    ImGuiIO& io = ImGui::GetIO();
    if (const FontFile* file = LoadFontFile({ "segoeui.ttf" }))
    {
        g_uiLinePerEm = file->linePerEm;
        g_fonts.ui = AddFont(*file, UiFontSize(), false, 1, excludePrivateUse);
        MergeFallbacks(UiFontSize(), file->linePerEm);
    }
    else
        g_fonts.ui = io.Fonts->AddFontDefault();

    if (const FontFile* file = LoadFontFile({ "seguisb.ttf", "segoeuib.ttf" }))
    {
        g_fonts.semibold = AddFont(*file, UiFontSize(), false, 1, excludePrivateUse);
        MergeFallbacks(UiFontSize(), file->linePerEm);
    }
    else
        g_fonts.semibold = g_fonts.ui;

    if (const FontFile* file = LoadFontFile({ "CascadiaMono.ttf", "consola.ttf", "cour.ttf" }))
    {
        g_monoLinePerEm = file->linePerEm;
        g_fonts.mono = AddFont(*file, MonoEm(14), false, 1, excludePrivateUse);
        MergeFallbacks(MonoEm(14), file->linePerEm);
    }
    else
        g_fonts.mono = g_fonts.ui;

    io.FontDefault = g_fonts.ui;
}

void Theme::Apply(bool dark, float dpiScale)
{
    g_palette = dark ? &DarkPalette : &LightPalette;
    g_dpiScale = dpiScale;
    const Palette& p = *g_palette;

    ImGuiStyle style;
    style.WindowPadding = ImVec2(20, 16);
    style.WindowRounding = 8;
    style.WindowBorderSize = 1;
    style.ChildRounding = 6;
    style.ChildBorderSize = 1;
    style.PopupRounding = 6;
    style.PopupBorderSize = 1;
    style.FramePadding = ImVec2(10, 7); // 32px-tall inputs, matching the buttons
    style.FrameRounding = 4;
    style.FrameBorderSize = 1;
    style.ItemSpacing = ImVec2(8, 8);
    style.ItemInnerSpacing = ImVec2(8, 6);
    style.CellPadding = ImVec2(6, 4);
    style.ScrollbarSize = 12;
    style.ScrollbarRounding = 6;
    style.GrabMinSize = 12;
    style.GrabRounding = 4;
    style.TabRounding = 0;
    style.TabBorderSize = 0;
    style.TabBarBorderSize = 0;
    style.TabBarOverlineSize = 0;
    style.TabCloseButtonMinWidthSelected = -1;
    style.TabCloseButtonMinWidthUnselected = -1;
    style.WindowTitleAlign = ImVec2(0, 0.5f);
    style.SeparatorTextBorderSize = 1;
    style.HoverDelayNormal = 0.5f;
    style.DisabledAlpha = 0.45f;

    ImVec4* c = style.Colors;
    c[ImGuiCol_Text] = V(p.text);
    c[ImGuiCol_TextDisabled] = V(p.textDim);
    c[ImGuiCol_WindowBg] = V(p.background);
    c[ImGuiCol_ChildBg] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_PopupBg] = V(p.card);
    c[ImGuiCol_Border] = V(p.border);
    c[ImGuiCol_BorderShadow] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_FrameBg] = V(p.inputBackground);
    c[ImGuiCol_FrameBgHovered] = V(p.inputHover);
    c[ImGuiCol_FrameBgActive] = V(p.inputBackground);
    c[ImGuiCol_TitleBg] = V(p.chrome);
    c[ImGuiCol_TitleBgActive] = V(p.chrome);
    c[ImGuiCol_TitleBgCollapsed] = V(p.chrome);
    c[ImGuiCol_MenuBarBg] = V(p.chrome);
    c[ImGuiCol_ScrollbarBg] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_ScrollbarGrab] = WithAlpha(p.textDim, 0.35f);
    c[ImGuiCol_ScrollbarGrabHovered] = WithAlpha(p.textDim, 0.55f);
    c[ImGuiCol_ScrollbarGrabActive] = WithAlpha(p.textDim, 0.7f);
    c[ImGuiCol_CheckMark] = V(p.accent);
    c[ImGuiCol_SliderGrab] = V(p.accentFill);
    c[ImGuiCol_SliderGrabActive] = V(p.accentFillHover);
    c[ImGuiCol_Button] = V(p.surface);
    c[ImGuiCol_ButtonHovered] = V(p.surfacePressed);
    c[ImGuiCol_ButtonActive] = V(p.border);
    c[ImGuiCol_Header] = V(p.selection);
    c[ImGuiCol_HeaderHovered] = V(p.surface);
    c[ImGuiCol_HeaderActive] = V(p.selectionHover);
    c[ImGuiCol_Separator] = V(p.border);
    c[ImGuiCol_SeparatorHovered] = V(p.border);
    c[ImGuiCol_SeparatorActive] = V(p.border);
    c[ImGuiCol_ResizeGrip] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_ResizeGripHovered] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_ResizeGripActive] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_InputTextCursor] = V(p.text);
    c[ImGuiCol_Tab] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_TabHovered] = V(p.surface);
    c[ImGuiCol_TabSelected] = V(p.background);
    c[ImGuiCol_TabSelectedOverline] = V(p.accent);
    c[ImGuiCol_TabDimmed] = ImVec4(0, 0, 0, 0);
    c[ImGuiCol_TabDimmedSelected] = V(p.background);
    c[ImGuiCol_TabDimmedSelectedOverline] = V(p.accent);
    c[ImGuiCol_TextLink] = V(p.accent);
    c[ImGuiCol_TextSelectedBg] = WithAlpha(p.accent, 0.35f);
    c[ImGuiCol_NavCursor] = V(p.accent);
    c[ImGuiCol_ModalWindowDimBg] = ImVec4(0, 0, 0, dark ? 0.55f : 0.3f);

    style.ScaleAllSizes(dpiScale);
    style.FontSizeBase = UiFontSize();
    style.FontScaleDpi = dpiScale;
    ImGui::GetStyle() = style;
}
