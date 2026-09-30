cmake_minimum_required(VERSION 3.20)
project(JTools VERSION 0.4.2 LANGUAGES CXX RC)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Optimize for size unless told otherwise.
if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE MinSizeRel CACHE STRING "Build type" FORCE)
endif()

set(IMGUI_DIR ${CMAKE_CURRENT_SOURCE_DIR}/third_party/imgui)

add_executable(JTools WIN32
    src/Common/Color.cpp
    src/Common/Json.cpp
    src/Common/Theme.cpp
    src/Common/Util.cpp
    src/Common/Widgets.cpp
    src/JTail/LogTailer.cpp
    src/JTail/LogView.cpp
    src/JTail/Stores.cpp
    src/JTail/TailView.cpp
    src/TextExpander/AppSettings.cpp
    src/TextExpander/ExpanderHost.cpp
    src/TextExpander/KeyboardHook.cpp
    src/TextExpander/SnippetStore.cpp
    src/TextExpander/StartupManager.cpp
    src/TextExpander/TextSender.cpp
    src/JTools/App.cpp
    src/JTools/ExpanderView.cpp
    src/JTools/main.cpp
    ${IMGUI_DIR}/imgui.cpp
    ${IMGUI_DIR}/imgui_draw.cpp
    ${IMGUI_DIR}/imgui_tables.cpp
    ${IMGUI_DIR}/imgui_widgets.cpp
    ${IMGUI_DIR}/imgui_impl_win32.cpp
    ${IMGUI_DIR}/imgui_impl_dx11.cpp
    ${IMGUI_DIR}/imgui_stdlib.cpp
    ${CMAKE_CURRENT_BINARY_DIR}/app.rc
)

# The app icon is shared with the C# project.
set(ICON_PATH ${CMAKE_CURRENT_SOURCE_DIR}/../JTools/Assets/icon.ico)
configure_file(res/app.rc.in ${CMAKE_CURRENT_BINARY_DIR}/app.rc @ONLY)

target_include_directories(JTools PRIVATE ${IMGUI_DIR})
target_compile_definitions(JTools PRIVATE
    UNICODE _UNICODE NOMINMAX _WIN32_WINNT=0x0A00
    JTOOLS_VERSION="${PROJECT_VERSION}"
    IMGUI_DISABLE_DEMO_WINDOWS IMGUI_DISABLE_DEBUG_TOOLS IMGUI_DISABLE_OBSOLETE_FUNCTIONS)

if(MSVC)
    target_link_libraries(JTools PRIVATE d3d11 d3dcompiler dxgi dwmapi shell32 ole32 uuid advapi32 user32 gdi32 imm32)
    # Static CRT, so the exe runs without the Visual C++ redistributable.
    set_property(TARGET JTools PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
    target_compile_options(JTools PRIVATE /utf-8 /W3 $<$<NOT:$<CONFIG:Debug>>:/O1 /Gy /GL>)
    target_link_options(JTools PRIVATE $<$<NOT:$<CONFIG:Debug>>:/LTCG /OPT:REF /OPT:ICF>)
else()
    # MinGW-w64 (GCC or Clang): fully static, stripped, unused code removed.
    target_link_libraries(JTools PRIVATE d3d11 d3dcompiler_47 dxgi dwmapi shell32 ole32 uuid advapi32 user32 gdi32 imm32)
    target_compile_options(JTools PRIVATE -municode -ffunction-sections -fdata-sections -Wall)
    # GCC 16 with -Os/-Oz in C++20 mode leaves std::string's move constructor unresolved at link time,
    # so size builds use -O2 (the later flag wins).
    target_compile_options(JTools PRIVATE $<$<CONFIG:MinSizeRel>:-O2>)
    target_link_options(JTools PRIVATE -municode -static $<$<NOT:$<CONFIG:Debug>>:-s -Wl,--gc-sections>)
endif()
