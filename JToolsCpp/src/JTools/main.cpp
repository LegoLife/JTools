// JTools: a small Windows tray app with two tools in one window: JTail (tail log files) and
// Text Expander (type an abbreviation anywhere and it expands).
//
// Runs from the system tray: closing the window only hides it, so text expansion keeps working.
// The app exits from the tray menu or JTail's More → Exit.
//
// The window is drawn with Dear ImGui on Direct3D 11. Frames are rendered only when something
// happens (input, a log file changing), so the app idles at ~0% CPU in the tray.

#include "App.h"
#include "Platform.h"

#include "../Common/Theme.h"
#include "../Common/Util.h"
#include "imgui.h"
#include "imgui_impl_dx11.h"
#include "imgui_impl_win32.h"

#include <windows.h>
#include <d3d11.h>
#include <dwmapi.h>
#include <shellapi.h>
#include <shobjidl.h>

#include <algorithm>
#include <memory>

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

namespace
{
    constexpr UINT WM_TRAY = WM_APP + 1;
    constexpr UINT_PTR PollTimerId = 1;
    constexpr UINT PollIntervalMs = 1000;
    constexpr int FramesAfterEvent = 3;
    const wchar_t* WindowClass = L"JTools.MainWindow";

    enum TrayCommand : UINT { CmdOpen = 1, CmdExpansion, CmdStartup, CmdExit };

    HINSTANCE g_instance;
    HWND g_hwnd;
    UINT g_taskbarCreated;
    std::unique_ptr<App> g_app;
    bool g_exiting = false;
    bool g_inFrame = false;
    int g_pendingFrames = FramesAfterEvent;
    std::vector<std::function<void()>> g_afterFrame;
    std::wstring g_title;

    ID3D11Device* g_device;
    ID3D11DeviceContext* g_context;
    IDXGISwapChain* g_swapChain;
    ID3D11RenderTargetView* g_renderTarget;

    // --- Direct3D ---

    void CreateRenderTarget()
    {
        ID3D11Texture2D* backBuffer = nullptr;
        g_swapChain->GetBuffer(0, IID_PPV_ARGS(&backBuffer));
        if (backBuffer)
        {
            g_device->CreateRenderTargetView(backBuffer, nullptr, &g_renderTarget);
            backBuffer->Release();
        }
    }

    void CleanupRenderTarget()
    {
        if (g_renderTarget)
        {
            g_renderTarget->Release();
            g_renderTarget = nullptr;
        }
    }

    bool CreateDevice(HWND hwnd)
    {
        DXGI_SWAP_CHAIN_DESC desc{};
        desc.BufferCount = 2;
        desc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        desc.OutputWindow = hwnd;
        desc.SampleDesc.Count = 1;
        desc.Windowed = TRUE;
        desc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

        const D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_0 };
        D3D_FEATURE_LEVEL level;
        HRESULT result = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, levels, 2,
                                                       D3D11_SDK_VERSION, &desc, &g_swapChain, &g_device, &level, &g_context);
        if (result == DXGI_ERROR_UNSUPPORTED) // no GPU driver: fall back to the software rasterizer
            result = D3D11CreateDeviceAndSwapChain(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, 0, levels, 2,
                                                   D3D11_SDK_VERSION, &desc, &g_swapChain, &g_device, &level, &g_context);
        if (FAILED(result))
            return false;

        CreateRenderTarget();
        return true;
    }

    void CleanupDevice()
    {
        CleanupRenderTarget();
        if (g_swapChain) g_swapChain->Release();
        if (g_context) g_context->Release();
        if (g_device) g_device->Release();
    }

    // --- Window ---

    void ApplyTitleBarTheme()
    {
        BOOL dark = Theme::IsDark();
        DwmSetWindowAttribute(g_hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, &dark, sizeof dark);
        COLORREF caption = Theme::Current().chrome & 0x00FFFFFF; // ImGui colors are 0xAABBGGRR, like COLORREF
        DwmSetWindowAttribute(g_hwnd, 35 /* DWMWA_CAPTION_COLOR */, &caption, sizeof caption);
    }

    void RenderFrame()
    {
        if (!g_app || g_inFrame)
            return;
        g_inFrame = true;

        float dpiScale = ImGui_ImplWin32_GetDpiScaleForHwnd(g_hwnd);
        bool dark = g_app->UsesDarkTheme();
        if (dpiScale != Theme::DpiScale() || dark != Theme::IsDark())
        {
            Theme::Apply(dark, dpiScale);
            ApplyTitleBarTheme();
        }

        ImGui_ImplDX11_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();
        g_app->Draw();
        ImGui::Render();

        std::wstring title = Widen(g_app->WindowTitle());
        if (title != g_title)
        {
            g_title = title;
            SetWindowTextW(g_hwnd, title.c_str());
        }

        ImVec4 clear = ImGui::ColorConvertU32ToFloat4(Theme::Current().background);
        const float clearColor[4] = { clear.x, clear.y, clear.z, 1 };
        g_context->OMSetRenderTargets(1, &g_renderTarget, nullptr);
        g_context->ClearRenderTargetView(g_renderTarget, clearColor);
        ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
        g_swapChain->Present(1, 0);

        g_inFrame = false;
    }

    void ShowMainWindow()
    {
        ShowWindow(g_hwnd, IsIconic(g_hwnd) ? SW_RESTORE : SW_SHOW);
        SetForegroundWindow(g_hwnd);
        g_pendingFrames = FramesAfterEvent;
    }

    void Exit()
    {
        g_exiting = true;
        PostQuitMessage(0);
    }

    // --- Tray icon ---

    NOTIFYICONDATAW TrayIconData()
    {
        NOTIFYICONDATAW data{};
        data.cbSize = sizeof data;
        data.hWnd = g_hwnd;
        data.uID = 1;
        return data;
    }

    void AddTrayIcon()
    {
        NOTIFYICONDATAW data = TrayIconData();
        data.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
        data.uCallbackMessage = WM_TRAY;
        data.hIcon = (HICON)LoadImageW(g_instance, MAKEINTRESOURCEW(1), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON),
                                       GetSystemMetrics(SM_CYSMICON), 0);
        wcscpy_s(data.szTip, L"JTools");
        Shell_NotifyIconW(NIM_ADD, &data);
    }

    void RemoveTrayIcon()
    {
        NOTIFYICONDATAW data = TrayIconData();
        Shell_NotifyIconW(NIM_DELETE, &data);
    }

    void ShowTrayMenu()
    {
        ExpanderHost& expander = g_app->Expander();
        StartupManager& startup = g_app->Startup();

        HMENU menu = CreatePopupMenu();
        AppendMenuW(menu, MF_STRING, CmdOpen, L"Open JTools");
        AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
        AppendMenuW(menu, MF_STRING | (expander.Enabled() ? MF_CHECKED : 0) | (expander.IsAvailable() ? 0 : MF_GRAYED),
                    CmdExpansion, L"Text expansion enabled");
        AppendMenuW(menu, MF_STRING | (startup.IsEnabled() ? MF_CHECKED : 0), CmdStartup, L"Start with Windows");
        AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
        AppendMenuW(menu, MF_STRING, CmdExit, L"Exit");
        SetMenuDefaultItem(menu, CmdOpen, FALSE);

        // Without the foreground switch the menu doesn't close when clicking elsewhere.
        POINT cursor;
        GetCursorPos(&cursor);
        SetForegroundWindow(g_hwnd);
        UINT command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, cursor.x, cursor.y, 0, g_hwnd,
                                      nullptr);
        PostMessageW(g_hwnd, WM_NULL, 0, 0);
        DestroyMenu(menu);

        switch (command)
        {
        case CmdOpen: ShowMainWindow(); break;
        case CmdExpansion: expander.SetEnabled(!expander.Enabled()); break;
        case CmdStartup: startup.SetEnabled(!startup.IsEnabled()); break;
        case CmdExit: Exit(); break;
        }
        g_pendingFrames = FramesAfterEvent;
    }

    std::vector<std::wstring> DroppedFiles(HDROP drop)
    {
        std::vector<std::wstring> paths;
        UINT count = DragQueryFileW(drop, 0xFFFFFFFF, nullptr, 0);
        for (UINT i = 0; i < count; i++)
        {
            std::wstring path(DragQueryFileW(drop, i, nullptr, 0), L'\0');
            DragQueryFileW(drop, i, path.data(), (UINT)path.size() + 1);
            paths.push_back(path);
        }
        DragFinish(drop);
        return paths;
    }

    LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        if (ImGui_ImplWin32_WndProcHandler(hwnd, msg, wParam, lParam))
            return 1;

        switch (msg)
        {
        case WM_SIZE:
            if (g_device && wParam != SIZE_MINIMIZED)
            {
                CleanupRenderTarget();
                g_swapChain->ResizeBuffers(0, LOWORD(lParam), HIWORD(lParam), DXGI_FORMAT_UNKNOWN, 0);
                CreateRenderTarget();
                RenderFrame(); // keep drawing while the user drags the window border
            }
            return 0;
        case WM_GETMINMAXINFO:
        {
            float scale = ImGui_ImplWin32_GetDpiScaleForHwnd(hwnd);
            auto* info = (MINMAXINFO*)lParam;
            info->ptMinTrackSize = { (LONG)(720 * scale), (LONG)(440 * scale) };
            return 0;
        }
        case WM_DPICHANGED:
        {
            const RECT* suggested = (const RECT*)lParam;
            SetWindowPos(hwnd, nullptr, suggested->left, suggested->top, suggested->right - suggested->left,
                         suggested->bottom - suggested->top, SWP_NOZORDER | SWP_NOACTIVATE);
            return 0;
        }
        case WM_TIMER:
            if (wParam == PollTimerId && g_app && g_app->Poll())
                g_pendingFrames = FramesAfterEvent;
            return 0;
        case WM_DROPFILES:
            if (g_app)
                g_app->OpenFiles(DroppedFiles((HDROP)wParam));
            return 0;
        case WM_CLOSE:
            // Clicking X hides to the tray so text expansion keeps running; exit from the tray menu.
            if (!g_exiting)
            {
                ShowWindow(hwnd, SW_HIDE);
                return 0;
            }
            break;
        case WM_TRAY:
            if (LOWORD(lParam) == WM_LBUTTONUP)
                ShowMainWindow();
            else if (LOWORD(lParam) == WM_RBUTTONUP)
                ShowTrayMenu();
            return 0;
        case WM_SYSCOMMAND:
            if ((wParam & 0xFFF0) == SC_KEYMENU) // no window menu on Alt; ImGui uses Alt for navigation
                return 0;
            break;
        case WM_ENDSESSION:
            if (wParam)
                Exit();
            return 0;
        case WM_ERASEBKGND:
            return 1;
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        default:
            if (msg == g_taskbarCreated) // Explorer restarted: the tray icon is gone
                AddTrayIcon();
            break;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    HWND CreateMainWindow()
    {
        WNDCLASSEXW windowClass{};
        windowClass.cbSize = sizeof windowClass;
        windowClass.lpfnWndProc = WindowProc;
        windowClass.hInstance = g_instance;
        windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass.hIcon = (HICON)LoadImageW(g_instance, MAKEINTRESOURCEW(1), IMAGE_ICON, GetSystemMetrics(SM_CXICON),
                                              GetSystemMetrics(SM_CYICON), 0);
        windowClass.hIconSm = (HICON)LoadImageW(g_instance, MAKEINTRESOURCEW(1), IMAGE_ICON,
                                                GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0);
        windowClass.lpszClassName = WindowClass;
        RegisterClassExW(&windowClass);

        // 1100 × 700, centered on the primary monitor's work area.
        RECT workArea;
        SystemParametersInfoW(SPI_GETWORKAREA, 0, &workArea, 0);
        float scale = ImGui_ImplWin32_GetDpiScaleForMonitor(MonitorFromPoint({ 0, 0 }, MONITOR_DEFAULTTOPRIMARY));
        int width = std::min((int)(1100 * scale), (int)(workArea.right - workArea.left));
        int height = std::min((int)(700 * scale), (int)(workArea.bottom - workArea.top));
        int x = workArea.left + (workArea.right - workArea.left - width) / 2;
        int y = workArea.top + (workArea.bottom - workArea.top - height) / 2;

        return CreateWindowExW(0, WindowClass, L"JTools", WS_OVERLAPPEDWINDOW, x, y, width, height, nullptr, nullptr,
                               g_instance, nullptr);
    }

    bool HasArgument(const wchar_t* argument)
    {
        int count = 0;
        LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &count);
        bool found = false;
        for (int i = 1; i < count && !found; i++)
            found = wcscmp(args[i], argument) == 0;
        LocalFree(args);
        return found;
    }
}

void Platform::RunAfterFrame(std::function<void()> action)
{
    g_afterFrame.push_back(std::move(action));
}

void Platform::RequestFrames()
{
    if (g_pendingFrames < FramesAfterEvent)
        g_pendingFrames = FramesAfterEvent;
}

std::vector<std::wstring> Platform::ShowOpenFilesDialog(const wchar_t* title)
{
    std::vector<std::wstring> paths;
    IFileOpenDialog* dialog = nullptr;
    if (FAILED(CoCreateInstance(CLSID_FileOpenDialog, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&dialog))))
        return paths;

    DWORD options = 0;
    dialog->GetOptions(&options);
    dialog->SetOptions(options | FOS_ALLOWMULTISELECT | FOS_FILEMUSTEXIST | FOS_FORCEFILESYSTEM);
    dialog->SetTitle(title);

    IShellItemArray* items = nullptr;
    if (SUCCEEDED(dialog->Show(g_hwnd)) && SUCCEEDED(dialog->GetResults(&items)))
    {
        DWORD count = 0;
        items->GetCount(&count);
        for (DWORD i = 0; i < count; i++)
        {
            IShellItem* item = nullptr;
            PWSTR path = nullptr;
            if (SUCCEEDED(items->GetItemAt(i, &item)) && SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                paths.emplace_back(path);
                CoTaskMemFree(path);
            }
            if (item)
                item->Release();
        }
        items->Release();
    }
    dialog->Release();
    return paths;
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int)
{
    g_instance = instance;
    ImGui_ImplWin32_EnableDpiAwareness();

    // Already running? It lives in the system tray.
    HANDLE singleInstance = CreateMutexW(nullptr, TRUE, L"Local\\JTools.SingleInstance");
    if (GetLastError() == ERROR_ALREADY_EXISTS)
        return 0;

    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    g_taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");
    g_hwnd = CreateMainWindow();
    if (!g_hwnd || !CreateDevice(g_hwnd))
    {
        MessageBoxW(nullptr, L"JTools couldn't start Direct3D 11.", L"JTools", MB_ICONERROR);
        return 1;
    }

    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = nullptr;
    io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
    io.ConfigInputTextCursorBlink = false; // a blinking caret would need a frame every half second
    ImGui_ImplWin32_Init(g_hwnd);
    ImGui_ImplDX11_Init(g_device, g_context);
    Theme::LoadFonts();

    g_app = std::make_unique<App>();
    Theme::Apply(g_app->UsesDarkTheme(), ImGui_ImplWin32_GetDpiScaleForHwnd(g_hwnd));
    ApplyTitleBarTheme();

    DragAcceptFiles(g_hwnd, TRUE);
    SetTimer(g_hwnd, PollTimerId, PollIntervalMs, nullptr);
    AddTrayIcon();
    if (!HasArgument(App::StartInTrayArgument))
        ShowMainWindow();

    while (true)
    {
        bool visible = IsWindowVisible(g_hwnd) && !IsIconic(g_hwnd);
        if (!visible || g_pendingFrames <= 0)
        {
            // Idle: sleep until a message arrives. While the window is open, still wake now and then
            // for tooltips and timed UI (e.g. the delete confirmation resetting).
            DWORD timeout = !visible ? INFINITE : GetForegroundWindow() == g_hwnd ? 100 : 500;
            MsgWaitForMultipleObjectsEx(0, nullptr, timeout, QS_ALLINPUT, MWMO_INPUTAVAILABLE);
        }

        bool quit = false, gotMessage = false;
        MSG msg;
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT)
                quit = true;
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
            gotMessage = true;
        }
        if (quit)
            break;
        if (gotMessage && g_pendingFrames < FramesAfterEvent)
            g_pendingFrames = FramesAfterEvent;

        if (!IsWindowVisible(g_hwnd) || IsIconic(g_hwnd))
        {
            g_pendingFrames = 0;
            continue;
        }

        RenderFrame();
        if (g_pendingFrames > 0)
            g_pendingFrames--;

        auto actions = std::move(g_afterFrame);
        g_afterFrame.clear();
        for (auto& action : actions)
            action();

        if (g_app->ExitRequested())
            Exit();
    }

    KillTimer(g_hwnd, PollTimerId);
    RemoveTrayIcon();
    g_app.reset(); // unhooks the keyboard and releases the expander lock
    ImGui_ImplDX11_Shutdown();
    ImGui_ImplWin32_Shutdown();
    ImGui::DestroyContext();
    CleanupDevice();
    DestroyWindow(g_hwnd);
    CoUninitialize();
    ReleaseMutex(singleInstance);
    CloseHandle(singleInstance);
    return 0;
}
