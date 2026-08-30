using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HomeAssistantDesktop.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HomeAssistantDesktop";
    private readonly ILogger _log;

    public StartupService(ILogger log) => _log = log;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var val = key?.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(val) && File.Exists(val.Trim('"')); 
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? "";
                key.SetValue(AppName, $"\"{exe}\"");
                _log.LogInformation("Auto-start enabled");
            }
            else
            {
                if (key.GetValue(AppName) is not null) key.DeleteValue(AppName);
                _log.LogInformation("Auto-start disabled");
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to change auto-start setting");
        }
    }

    public void SyncWithSettings(bool startWithWindows)
    {
        if (IsEnabled != startWithWindows) SetEnabled(startWithWindows);
    }
}
