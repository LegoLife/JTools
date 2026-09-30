# JTools (C++)

A C++ port of JTools: the same tray app with **Tail** (a live log viewer) and **Expand** (a text expander), rebuilt on Win32 and [Dear ImGui](https://github.com/ocornut/imgui) instead of .NET and Avalonia.

| | C# (.NET 10 + Avalonia) | C++ (Win32 + Dear ImGui) |
| --- | --- | --- |
| `JTools.exe` | 171 MB (self-contained, ReadyToRun) | **1.2 MB** |
| Runtime needed | none (bundled) | none (Windows 10/11 system DLLs only) |

## Compatibility with the C# app

- Uses the same settings files in `%APPDATA%` (`JTail\*.json`, `JTools\settings.json`, `TextExpander\*.json`). Everything you set up in one version carries over to the other.
- Uses the same single-instance and "Start with Windows" names, so only one of the two versions runs at a time.

## Building

Requires CMake 3.20+ and a C++20 compiler. Built and tested with MinGW-w64 GCC 16.2. The CMake file also has Visual Studio 2022 settings (static CRT, size optimizations), but that build hasn't been tried yet.

MinGW-w64 (e.g. `scoop install mingw cmake ninja`):

```
cmake -S . -B build -G Ninja
cmake --build build
```

Visual Studio (from a Developer Command Prompt):

```
cmake -S . -B build
cmake --build build --config MinSizeRel
```

The result is a single `JTools.exe` (in `build/`, or `build/MinSizeRel/` for Visual Studio), statically linked, with no DLLs to ship.

## How it's put together

```
src/
  Common/        JSON, colors, theme/palette, fonts, shared widgets
  JTail/         log tailing: file reader, virtualized log view, toolbar/tabs/status bar, dialogs
  TextExpander/  keyboard hook, clipboard paste, snippets and settings, "Start with Windows"
  JTools/        main window (side bar, theme menu), Text Expander UI, Win32/Direct3D host (main.cpp)
third_party/imgui/  Dear ImGui 1.92.9 (MIT), vendored unchanged
```

- **Small on disk**: fonts (Segoe UI, Segoe Fluent Icons, Cascadia Mono or Consolas) load from `C:\Windows\Fonts`, so none are embedded.
- **Idle in the tray**: frames are drawn only when something happens (input, or a tailed file changing). Hidden in the tray, the app sleeps until a message arrives. Tailed files are polled once a second, as in the C# app.
- **Responsive typing**: the keyboard hook runs on its own thread, and each expansion is pasted from a background thread, so typing never waits on the UI.
- **Large logs**: the log view draws only the visible lines, so opening a big file costs memory, not frame time.

## Differences from the C# app

- Dialogs (Highlights, Log Appearance, Text Expansion Settings) open inside the main window instead of as separate windows.
- Colors are picked with Dear ImGui's color picker.
- Case-insensitive matching (highlight words, the abbreviation search) folds ASCII letters only.
- In the log view you can double-click to select a word, and right-click for Copy / Select All.
