using System.IO;
using HomeAssistantDesktop.Models;
using HomeAssistantDesktop.Resources;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class ServerManager
{
    private readonly SettingsService _settings;
    private readonly ILogger _log;

    public ServerManager(SettingsService settings, ILogger log)
    {
        _settings = settings;
        _log = log;
    }

    public string ProfileDirectoryFor(ServerProfile server)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAssistantDesktop", "WebView2");
        var safe = "server-" + server.Id;
        return Path.Combine(root, safe);
    }

    public ServerProfile? GetActive() => _settings.Settings.ActiveServer;

    public void SetActive(string id)
    {
        _settings.Settings.ActiveServerId = id;
        _settings.Save();
        _log.LogInformation("Active server set to {Id}", id);
    }

    public void AddOrUpdate(ServerProfile server)
    {
        var list = _settings.Settings.Servers;
        var existing = list.FirstOrDefault(s => s.Id == server.Id);
        if (existing is null)
        {
            if (list.Count == 0) server.IsDefault = true;
            list.Add(server);
        }
        else
        {
            existing.Name = server.Name;
            existing.Url = server.Url;
        }
        _settings.Save();
    }

    public void Delete(string id)
    {
        var list = _settings.Settings.Servers;
        var target = list.FirstOrDefault(s => s.Id == id);
        if (target is null) return;
        list.Remove(target);
        if (_settings.Settings.ActiveServerId == id)
            _settings.Settings.ActiveServerId = list.FirstOrDefault()?.Id;
        _settings.Save();
    }

    public void SetDefault(string id)
    {
        foreach (var s in _settings.Settings.Servers) s.IsDefault = s.Id == id;
        _settings.Save();
    }
}
