# Home Assistant Desktop

A native Windows desktop client for Home Assistant, built with C#, WPF, and WebView2. Provides quick access to the Home Assistant dashboard with built-in features like always-on-top, multi-server support, zoom persistence, and system tray integration.

[![Download MSI](https://img.shields.io/badge/Download-Installer-blue)](https://github.com/Owie2711/HOME-ASSISTANT-APP/releases/download/v0.1.0/HomeAssistantDesktopSetup.msi)

## Features

- **Full dashboard** — Renders the native Home Assistant UI through WebView2
- **Multi-server** — Add, edit, delete, and switch between Home Assistant servers
- **Brutal always-on-top** — Stays above all windows, survives Win+D / Show Desktop
- **Zoom persistence** — Zoom changes (Ctrl+/-) are saved automatically per server
- **System tray** — Minimize to tray, quick access, server switching from tray menu
- **Startup with Windows** — Optional auto-start via Windows Registry
- **Window state persistence** — Size, position, and maximized state are restored on launch
- **Fullscreen** — Press F11 to toggle
- **First-run setup** — Guided wizard on first launch to configure your server
- **MSI installer** — Installs to `C:\Program Files\Home Assistant Desktop App` with Desktop & Start Menu shortcuts
- **Self-contained** — No .NET runtime installation required

## Requirements

- Windows 10/11 (x64)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (pre-installed on Windows 10 20H2+ and Windows 11)

## Installation

Download `HomeAssistantDesktopSetup.msi` from [Releases](https://github.com/Owie2711/HOME-ASSISTANT-APP/releases), run it, and follow the wizard. The app installs to `C:\Program Files\Home Assistant Desktop App`.

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [WiX Toolset v4](https://wixtoolset.org/) (for building the MSI installer)

### Steps

```powershell
# Clone the repo
git clone https://github.com/Owie2711/HOME-ASSISTANT-APP.git
cd HOME-ASSISTANT-APP

# Publish (self-contained single-file executable)
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o publish

# Build the MSI installer
dotnet tool install --global wix
cd installer
wix build Product.wxs -arch x64 -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o HomeAssistantDesktopSetup.msi
```

The published output goes to `publish/` and the installer to `installer/HomeAssistantDesktopSetup.msi`.

## Configuration

| File | Location |
|------|----------|
| Settings | `%LOCALAPPDATA%\HomeAssistantDesktop\Config\settings.json` |
| WebView2 data (per server) | `%LOCALAPPDATA%\HomeAssistantDesktop\WebView2\server-<id>` |
| Logs | `%LOCALAPPDATA%\HomeAssistantDesktop\Logs\ha-desktop-YYYY-MM-DD.log` |

## Project Structure

```
├── App.xaml / App.xaml.cs          # Application entry point, DI container setup
├── Models/
│   ├── AppSettings.cs              # Settings POCO (serialized to JSON)
│   └── ServerProfile.cs            # Server profile model
├── Services/
│   ├── FileLogger.cs               # Buffered file logger with daily rotation
│   ├── ServerManager.cs            # Server CRUD operations
│   ├── SettingsService.cs          # JSON settings persistence with debounced save
│   ├── SingleInstanceService.cs    # Named Mutex single-instance guard
│   ├── StartupService.cs           # Windows auto-start via Registry
│   ├── TrayService.cs              # System tray with server switching menu
│   ├── WebViewService.cs           # WebView2 lifecycle, connection probing
│   └── WindowService.cs            # Window state, brutal always-on-top via Win32
├── ViewModels/
│   ├── MainViewModel.cs            # Main window ViewModel
│   └── SettingsViewModel.cs        # Settings window ViewModel
├── Views/
│   ├── MainWindow.xaml / .cs       # Main window with WebView2 and overlays
│   └── SettingsWindow.xaml / .cs   # Settings dialog
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── Resources/
│   ├── Strings.resx                # Localized UI strings (English)
│   └── Strings.Designer.cs         # Strongly-typed resource accessor
└── installer/
    └── Product.wxs                 # WiX installer definition
```

## Architecture

- **DI Container** — Services are registered via `Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`
- **MVVM** — ViewModels use [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) with source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **Brutal always-on-top** — Win32 interop intercepts `WM_WINDOWPOSCHANGING`, `WM_SHOWWINDOW`, `WM_SIZE`, and `WM_ACTIVATE` to force `HWND_TOPMOST` + `WS_EX_TOOLWINDOW`
- **Connection probing** — Background HTTP probe on navigation start with cancellation token management

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| F11 | Toggle fullscreen |
| Escape | Close settings window |

## License

MIT
