using System.IO;
using System.Windows;
using System.Windows.Forms;
using HomeAssistantDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notify;
    private readonly SettingsService _settings;
    private readonly ILogger _log;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action<string>? ServerSelected;

    public TrayService(SettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
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
        menu.Items.Add("Open Home Assistant", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());

        var current = new ToolStripMenuItem("Current Server");
        RebuildServerMenu(current);
        menu.Items.Add(current);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());
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
            target.DropDownItems.Add(new ToolStripMenuItem("(no servers)") { Enabled = false });
    }

    public void ShowBalloon(string title, string text)
    {
        try { _notify.ShowBalloonTip(3000, title, text, ToolTipIcon.Info); } catch { }
    }

    public void Dispose()
    {
        _notify.Visible = false;
        _notify.Dispose();
        _log.LogInformation("Tray disposed");
    }
}
