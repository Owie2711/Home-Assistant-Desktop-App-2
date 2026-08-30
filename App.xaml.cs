using System.Windows;
using HomeAssistantDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop;

public partial class App : System.Windows.Application
{
    public static SettingsService Settings { get; private set; } = null!;
    public static ServerManager Servers { get; private set; } = null!;
    public static WebViewService WebView { get; private set; } = null!;
    public static WindowService Window { get; private set; } = null!;
    public static TrayService Tray { get; private set; } = null!;
    public static StartupService AutoStart { get; private set; } = null!;
    public static ILogger Log { get; private set; } = null!;
    public static SingleInstanceService SingleInstance { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var factory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider()).SetMinimumLevel(LogLevel.Information));
        Log = factory.CreateLogger("App");

        SingleInstance = new SingleInstanceService(Log);
        if (!SingleInstance.IsFirstInstance)
        {
            Log.LogInformation("Another instance already running; exiting");
            Shutdown();
            return;
        }

        Settings = new SettingsService(Log);
        Settings.Load();
        Servers = new ServerManager(Settings, Log);
        WebView = new WebViewService(Settings, Servers, Log);
        Window = new WindowService(Settings, Log);
        AutoStart = new StartupService(Log);
        AutoStart.SyncWithSettings(Settings.Settings.StartWithWindows);
        Tray = new TrayService(Settings, Log);

        Log.LogInformation("Application startup");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.LogInformation("Application shutdown");
        Tray.Dispose();
        SingleInstance.Dispose();
        base.OnExit(e);
    }
}
