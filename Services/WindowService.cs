using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HomeAssistantDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class WindowService
{
    private const int GWL_EXSTYLE = -20;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    private readonly SettingsService _settings;
    private readonly ILogger _log;
    private Window? _window;

    public WindowService(SettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log;
    }

    public void Attach(Window window)
    {
        _window = window;
        var s = _settings.Settings;
        window.Topmost = s.AlwaysOnTop;
        if (s.AlwaysOnTop) SetBrutalTopMost(true);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) => GetWindowLongPtr64(hWnd, nIndex);
    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) => SetWindowLongPtr64(hWnd, nIndex, dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private void SetBrutalTopMost(bool enable)
    {
        if (_window is null) return;
        var hwnd = new WindowInteropHelper(_window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = (uint)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if (enable)
            style |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
        else
            style &= ~(WS_EX_TOPMOST | WS_EX_TOOLWINDOW);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)style);
        if (enable)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void ApplySavedState()
    {
        if (_window is null) return;
        var s = _settings.Settings;
        if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
        {
            // ensure visible on an existing monitor
            var rect = new Rect(s.WindowLeft.Value, s.WindowTop.Value, s.WindowWidth, s.WindowHeight);
            if (IsOnScreen(rect))
            {
                _window.Left = s.WindowLeft.Value;
                _window.Top = s.WindowTop.Value;
            }
        }
        _window.Width = s.WindowWidth;
        _window.Height = s.WindowHeight;
        if (s.WindowMaximized) _window.WindowState = WindowState.Maximized;
        if (s.StartMinimized) _window.WindowState = WindowState.Minimized;
    }

    public void SaveState()
    {
        if (_window is null) return;
        var s = _settings.Settings;
        if (_window.WindowState == WindowState.Maximized)
        {
            s.WindowMaximized = true;
            // use RestoreBounds for real position/size
            var rb = _window.RestoreBounds;
            s.WindowLeft = rb.Left;
            s.WindowTop = rb.Top;
            s.WindowWidth = rb.Width;
            s.WindowHeight = rb.Height;
        }
        else
        {
            s.WindowMaximized = false;
            s.WindowLeft = _window.Left;
            s.WindowTop = _window.Top;
            s.WindowWidth = _window.Width;
            s.WindowHeight = _window.Height;
        }
        _settings.Save();
        _log.LogInformation("Window state saved");
    }

    public void SetAlwaysOnTop(bool value)
    {
        if (_window is null) return;
        _window.Topmost = value;
        SetBrutalTopMost(value);
    }

    public void ToggleFullscreen()
    {
        if (_window is null) return;
        var s = _settings.Settings;
        if (s.Fullscreen)
        {
            s.Fullscreen = false;
            _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.WindowState = s.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
        }
        else
        {
            s.Fullscreen = true;
            _window.WindowStyle = WindowStyle.None;
            _window.WindowState = WindowState.Maximized;
        }
        _settings.Save();
    }

    public void ApplyFullscreenOnLoad()
    {
        if (_window is null) return;
        var s = _settings.Settings;
        if (s.Fullscreen)
        {
            _window.WindowStyle = WindowStyle.None;
            _window.WindowState = WindowState.Maximized;
        }
    }

    private static bool IsOnScreen(Rect rect)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        foreach (var screen in screens)
        {
            var wa = screen.WorkingArea;
            if (rect.Left < wa.Right && rect.Right > wa.Left &&
                rect.Top < wa.Bottom && rect.Bottom > wa.Top)
                return true;
        }
        return false;
    }
}
