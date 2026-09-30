#pragma once

#include "imgui.h"

#include <cstdint>
#include <string>
#include <string_view>

/// An ARGB color, stored in the settings files as "#aarrggbb" (or a name such as "Black", which
/// Avalonia's Color.ToString() writes for known colors).
struct Color
{
    uint8_t a = 255, r = 0, g = 0, b = 0;

    static constexpr Color FromRgb(uint32_t rgb, uint8_t alpha = 255)
    {
        return { alpha, (uint8_t)(rgb >> 16), (uint8_t)(rgb >> 8), (uint8_t)rgb };
    }

    constexpr ImU32 ToU32() const { return IM_COL32(r, g, b, a); }
    ImVec4 ToVec4() const { return { r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f }; }

    static Color FromVec4(const ImVec4& v)
    {
        auto channel = [](float f) { return (uint8_t)(f <= 0 ? 0 : f >= 1 ? 255 : (int)(f * 255.0f + 0.5f)); };
        return { channel(v.w), channel(v.x), channel(v.y), channel(v.z) };
    }

    bool operator==(const Color& o) const { return a == o.a && r == o.r && g == o.g && b == o.b; }

    /// "#aarrggbb"
    std::string ToString() const;

    /// Accepts #RGB, #ARGB, #RRGGBB, #AARRGGBB and color names such as "Black".
    static bool Parse(std::string_view s, Color& out);
};
