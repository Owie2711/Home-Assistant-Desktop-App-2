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
    private IntPtr _hwnd;

    public WindowService(SettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log;
    }

    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MINIMIZE = 0xF020;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_SHOWWINDOW = 0x0018;
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private const int WM_ACTIVATE = 0x0006;
    private const int WM_ACTIVATEAPP = 0x001C;
    private const int WM_SIZE = 0x0005;
    private const int SIZE_MAXIMIZED = 2;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_NOZORDER = 0x0004;
    private volatile int _applyingTopMost;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    public void Attach(Window window)
    {
        _window = window;
        var s = _settings.Settings;
        window.Topmost = s.AlwaysOnTop;
        window.SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);
            if (s.AlwaysOnTop) SetBrutalTopMost(true);
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_settings.Settings.AlwaysOnTop) return IntPtr.Zero;

        switch (msg)
        {
            case WM_SYSCOMMAND:
                if ((wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
                    handled = true;
                break;

            case WM_SHOWWINDOW:
                if (wParam == IntPtr.Zero)
                    handled = true;
                break;
            case WM_SIZE:
                if (wParam.ToInt32() == SIZE_MAXIMIZED && Interlocked.CompareExchange(ref _applyingTopMost, 1, 0) == 0)
                {
                    SetBrutalTopMost(true);
                    Interlocked.Exchange(ref _applyingTopMost, 0);
                }
                break;

            case WM_WINDOWPOSCHANGING:
                if (lParam != IntPtr.Zero)
                {
                    var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                    pos.hwndInsertAfter = HWND_TOPMOST;
                    pos.flags &= ~(SWP_HIDEWINDOW | SWP_NOZORDER);
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                if (Interlocked.CompareExchange(ref _applyingTopMost, 1, 0) == 0)
                {
                    SetBrutalTopMost(true);
                    Interlocked.Exchange(ref _applyingTopMost, 0);
                }
                break;

            case WM_WINDOWPOSCHANGED:
            case WM_ACTIVATE:
            case WM_ACTIVATEAPP:
                if (Interlocked.CompareExchange(ref _applyingTopMost, 1, 0) == 0)
                {
                    SetBrutalTopMost(true);
                    Interlocked.Exchange(ref _applyingTopMost, 0);
                }
                break;
        }
        return IntPtr.Zero;
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
        if (_hwnd == IntPtr.Zero)
        {
            if (_window is not null) _hwnd = new WindowInteropHelper(_window).Handle;
        }
        if (_hwnd == IntPtr.Zero) return;
        var style = (uint)GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
        if (enable)
            style |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW;
        else
            style &= ~(WS_EX_TOPMOST | WS_EX_TOOLWINDOW);
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, (IntPtr)style);
        if (enable)
            SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
        bool changed = false;
        if (_window.WindowState == WindowState.Maximized)
        {
            var rb = _window.RestoreBounds;
            if (s.WindowMaximized != true || s.WindowLeft != rb.Left || s.WindowTop != rb.Top ||
                s.WindowWidth != rb.Width || s.WindowHeight != rb.Height)
            {
                s.WindowMaximized = true;
                s.WindowLeft = rb.Left;
                s.WindowTop = rb.Top;
                s.WindowWidth = rb.Width;
                s.WindowHeight = rb.Height;
                changed = true;
            }
        }
        else
        {
            if (s.WindowMaximized != false || s.WindowLeft != _window.Left || s.WindowTop != _window.Top ||
                s.WindowWidth != _window.Width || s.WindowHeight != _window.Height)
            {
                s.WindowMaximized = false;
                s.WindowLeft = _window.Left;
                s.WindowTop = _window.Top;
                s.WindowWidth = _window.Width;
                s.WindowHeight = _window.Height;
                changed = true;
            }
        }
        if (changed)
        {
            _settings.Save();
            _log.LogInformation("Window state saved");
        }
    }

    public void SetAlwaysOnTop(bool value)
    {
        if (_window is null) return;
        _window.Topmost = value;
        SetBrutalTopMost(value);
    }

    public void RestoreFromMinimize()
    {
        if (_window is null) return;
        var s = _settings.Settings;
        _window.WindowState = s.WindowMaximized ? WindowState.Maximized : WindowState.Normal;
        SetBrutalTopMost(true);
    }

    public void RestoreVisibility()
    {
        if (_window is null) return;
        if (_window.Visibility != Visibility.Visible)
            _window.Visibility = Visibility.Visible;
        SetBrutalTopMost(true);
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
