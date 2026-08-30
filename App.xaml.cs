using System.Windows;
using HomeAssistantDesktop.Services;
using HomeAssistantDesktop.ViewModels;
using HomeAssistantDesktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop;

public partial class App : System.Windows.Application
{
    // Backward-compatible statics — these are the same singleton instances from the DI container.
    // Prefer constructor injection in new code; these exist for places not yet refactored.
    public static SettingsService Settings { get; private set; } = null!;
    public static ServerManager Servers { get; private set; } = null!;
    public static WebViewService WebView { get; private set; } = null!;
    public static WindowService Window { get; private set; } = null!;
    public static TrayService Tray { get; private set; } = null!;
    public static StartupService AutoStart { get; private set; } = null!;
    public static ILogger Log { get; private set; } = null!;
    public static SingleInstanceService SingleInstance { get; private set; } = null!;

    private IHost? _host;

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

        // Build DI container
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Infrastructure
                services.AddSingleton<ILogger>(Log);
                services.AddSingleton(factory);

                // Core services
                services.AddSingleton<SettingsService>();
                services.AddSingleton<ServerManager>();
                services.AddSingleton<WebViewService>();
                services.AddSingleton<WindowService>();
                services.AddSingleton<StartupService>();
                services.AddSingleton<TrayService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<SettingsViewModel>();
            })
            .Build();

        var sp = _host.Services;

        // Resolve and wire up all services (DI container ensures singletons)
        Settings = sp.GetRequiredService<SettingsService>();
        Settings.Load();
        Servers = sp.GetRequiredService<ServerManager>();
        WebView = sp.GetRequiredService<WebViewService>();
        Window = sp.GetRequiredService<WindowService>();
        AutoStart = sp.GetRequiredService<StartupService>();
        AutoStart.SyncWithSettings(Settings.Settings.StartWithWindows);
        Tray = sp.GetRequiredService<TrayService>();

        Log.LogInformation("Application startup");

        // Create and show main window via DI
        var mainWindow = new MainWindow(
            sp.GetRequiredService<MainViewModel>(), sp, Log);
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.LogInformation("Application shutdown");
        Tray?.Dispose();
        SingleInstance?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
