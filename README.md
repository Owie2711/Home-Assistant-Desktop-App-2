# Home Assistant Desktop

Native Windows desktop client untuk Home Assistant, dibangun dengan C#, WPF, dan WebView2. Menyediakan akses cepat ke dashboard Home Assistant dengan fitur-fitur bawaan seperti always-on-top, multi-server, zoom persistence, dan system tray.

## Fitur

- **Dashboard penuh**: Menampilkan antarmuka Home Assistant asli melalui WebView2
- **Multi-server**: Tambah, edit, hapus, dan beralih antar server Home Assistant
- **Always-on-top brutal**: Tetap di atas semua jendela, tahan terhadap Win+D / Show Desktop
- **Zoom persistence**: Perubahan zoom (Ctrl+/-) tersimpan otomatis
- **System tray**: Minimize ke tray, akses cepat, server switching dari tray
- **Startup registry**: Opsi start with Windows
- **Window state persistence**: Ukuran, posisi, dan maximized state tersimpan
- **Fullscreen**: Tekan F11
- **Installer MSI**: Instalasi ke `C:\Program Files\Home Assistant Desktop App`, shortcut desktop & start menu

## Teknologi

- .NET 10 / C# / WPF
- WebView2 Runtime (harus terinstall)
- CommunityToolkit.Mvvm
- WiX Toolset v4 (untuk installer)

## Persyaratan

- Windows 10/11 (x64)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

## Instalasi

Unduh `HomeAssistantDesktopSetup.msi` dari [Releases](https://github.com/Owie2711/HOME-ASSISTANT-APP/releases), jalankan, ikuti wizard. Aplikasi akan terinstal di `C:\Program Files\Home Assistant Desktop App`.

## Build dari source

```powershell
# Clone repo
git clone https://github.com/Owie2711/HOME-ASSISTANT-APP.git
cd HOME-ASSISTANT-APP

# Restore & build/publish
dotnet restore
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o publish

# Build installer (perlu WiX v4)
dotnet tool install --global wix
cd installer
wix build Product.wxs -arch x64 -ext WixToolset.UI.wixext -o HomeAssistantDesktopSetup.msi
```

## Konfigurasi

Pengaturan disimpan di `%LOCALAPPDATA%\HomeAssistantDesktop\Config\settings.json`. Data WebView2 per-server di `%LOCALAPPDATA%\HomeAssistantDesktop\WebView2\server-<id>`.

## Lisensi

MIT