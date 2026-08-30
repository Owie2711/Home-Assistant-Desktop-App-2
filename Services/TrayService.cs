using System.IO;
using System.Windows;
using System.Windows.Forms;
using HomeAssistantDesktop.Models;
using HomeAssistantDesktop.Resources;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly SettingsService _settings;
    private readonly ILogger _log;
    private readonly List<ToolStripItem> _menuItems = new();

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<string>? ServerSelected;

    public TrayService(SettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Home Assistant.ico");
        if (!File.Exists(iconPath)) iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        _notify = new NotifyIcon
        {
            Text = "Home Assistant Desktop",
            Icon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notify.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var openItem = menu.Items.Add(Strings.Tray_Open, null, (_, _) => OpenRequested?.Invoke());
        _menuItems.Add(openItem);
        var sep1 = new ToolStripSeparator();
        menu.Items.Add(sep1);
        _menuItems.Add(sep1);

        var current = new ToolStripMenuItem(Strings.Tray_CurrentServer);
        RebuildServerMenu(current);
        menu.Items.Add(current);
        _menuItems.Add(current);

        var sep2 = new ToolStripSeparator();
        menu.Items.Add(sep2);
        _menuItems.Add(sep2);
        var settingsItem = menu.Items.Add(Strings.Tray_Settings, null, (_, _) => SettingsRequested?.Invoke());
        _menuItems.Add(settingsItem);
        var sep3 = new ToolStripSeparator();
        menu.Items.Add(sep3);
        _menuItems.Add(sep3);
        var exitItem = menu.Items.Add(Strings.Tray_Exit, null, (_, _) => ExitRequested?.Invoke());
        _menuItems.Add(exitItem);
        _currentServerMenu = current;
        return menu;
    }

    private ToolStripMenuItem? _currentServerMenu;

    public void RebuildServerMenu(ToolStripMenuItem? parent = null)
    {
        var target = parent ?? _currentServerMenu;
        if (target is null) return;
        target.DropDownItems.Clear();
        var active = _settings.Settings.ActiveServer;
        foreach (var s in _settings.Settings.Servers)
        {
            var item = new ToolStripMenuItem(
                $"{(s == active ? "● " : "○ ")}{s.Name}  ({s.Url})",
                null, (_, _) => ServerSelected?.Invoke(s.Id));
            target.DropDownItems.Add(item);
        }
        if (_settings.Settings.Servers.Count == 0)
            target.DropDownItems.Add(new ToolStripMenuItem(Strings.Tray_NoServers) { Enabled = false });
    }

    public void ShowBalloon(string title, string text)
    {
        try { _notify.ShowBalloonTip(3000, title, text, ToolTipIcon.Info); } catch { }
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        foreach (var item in _menuItems)
        {
            item.Dispose();
        }
        _menuItems.Clear();
        _log.LogInformation("Tray disposed");
    }
}
