using System.IO;
using System.Text.Json;
using HomeAssistantDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _configDir;
    private readonly string _filePath;
    private readonly ILogger _log;

    public AppSettings Settings { get; private set; } = new();

    public SettingsService(ILogger log)
    {
        _log = log;
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HomeAssistantDesktop", "Config");
        _filePath = Path.Combine(_configDir, "settings.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    Settings = loaded;
                    _log.LogInformation("Settings loaded from {Path}", _filePath);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to load settings, using defaults");
        }

        SeedDefaults();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Settings, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to save settings");
        }
    }

    private void SeedDefaults()
    {
        Settings.Servers.Add(new ServerProfile
        {
            Name = "Home",
            Url = "http://10.0.0.114:8123",
            IsDefault = true
        });
        Settings.ActiveServerId = Settings.Servers[0].Id;
    }
}
