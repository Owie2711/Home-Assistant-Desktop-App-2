using System.IO;
using System.Text.Json;
using HomeAssistantDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _lock = new();

    private readonly string _configDir;
    private readonly string _filePath;
    private readonly ILogger _log;
    private System.Threading.Timer? _debounceTimer;

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
        lock (_lock)
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

            _log.LogInformation("No existing settings, starting fresh");
        }
    }

    public void Save()
    {
        // Debounce: rapid successive saves write only the last state within a 500ms window
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ => SaveNow(), null, 500, Timeout.Infinite);
    }

    /// <summary>
    /// Writes settings to disk immediately, bypassing debounce.
    /// Use this when the app is about to exit and the debounce timer won't have time to fire.
    /// </summary>
    public void SaveNowImmediate()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        SaveNow();
    }

    private void SaveNow()
    {
        lock (_lock)
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
    }
}
