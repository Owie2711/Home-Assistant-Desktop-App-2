using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeAssistantDesktop.Models;
using HomeAssistantDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly WebViewService _webView;
    private readonly WindowService _window;
    private readonly SettingsService _settings;
    private readonly ServerManager _servers;
    private readonly TrayService _tray;
    private readonly ILogger _log;
    private string? _currentServerId;

    [ObservableProperty] private string _connectionText = "Connecting...";
    [ObservableProperty] private string _activeServerName = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _showError;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private bool _errorMode;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private string _setupUrl = "http://";
    [ObservableProperty] private bool _setupError;

    public MainViewModel(WebViewService webView, WindowService window, SettingsService settings,
        ServerManager servers, TrayService tray, ILogger log)
    {
        _webView = webView;
        _window = window;
        _settings = settings;
        _servers = servers;
        _tray = tray;
        _log = log;

        _webView.ConnectionStateChanged += OnConnectionStateChanged;
        _tray.ServerSelected += id => _ = SwitchServerAsync(id);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var server = _servers.GetActive();
            if (server is null)
            {
                IsFirstRun = true;
                ActiveServerName = "";
                return;
            }
            IsFirstRun = false;
            ActiveServerName = server.Name;
            _currentServerId = server.Id;
            await _webView.InitializeAsync();
            _window.ApplyFullscreenOnLoad();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize main view");
            ErrorMode = true;
            ShowError = true;
            ErrorText = "WebView2 could not be initialized.\n\nPlease install or update the Microsoft Edge WebView2 Runtime.";
        }
    }

    private void OnConnectionStateChanged(ConnectionState state)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectionText = state switch
            {
                ConnectionState.Connected => "● Connected",
                ConnectionState.Connecting => "● Connecting...",
                ConnectionState.Offline => "● Offline",
                _ => "● —"
            };
            ErrorMode = state == ConnectionState.Offline;
            if (state == ConnectionState.Connecting) IsLoading = true;
            else IsLoading = false;
        });
    }

    public async Task SwitchServerAsync(string id)
    {
        try
        {
            ShowError = false;
            await _webView.SwitchServerAsync(id);
            _currentServerId = id;
            var s = _settings.Settings.Servers.FirstOrDefault(x => x.Id == id);
            ActiveServerName = s?.Name ?? _servers.GetActive()?.Name ?? "";
            _tray.RebuildServerMenu();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Server switch failed");
            ErrorMode = true;
            ShowError = true;
            ErrorText = "WebView2 could not be initialized for the selected server.";
        }
    }

    public ICommand ReloadCommand => new RelayCommand(() => _webView.Reload());
    public ICommand SettingsCommand => new RelayCommand(() => SettingsRequested?.Invoke());
    public ICommand FullscreenCommand => new RelayCommand(() => _window.ToggleFullscreen());
    public ICommand RetryCommand => new RelayCommand(() => _ = _webView.NavigateToServerAsync());
    public AsyncRelayCommand SetupCommand => new(ExecuteSetupAsync);

    public event Action? SettingsRequested;

    public void SetLoading(bool loading) => IsLoading = loading;

    public async Task OnSettingsClosedAsync()
    {
        _tray.RebuildServerMenu();
        var server = _servers.GetActive();
        if (server is not null && server.Id != _currentServerId)
        {
            ActiveServerName = server.Name;
            await _webView.SwitchServerAsync(server.Id);
            _currentServerId = server.Id;
        }
    }

    private async Task ExecuteSetupAsync()
    {
        if (string.IsNullOrWhiteSpace(SetupUrl)) return;
        if (!Uri.TryCreate(SetupUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            ErrorText = "URL tidak valid. Gunakan format http://host:port";
            SetupError = true;
            ShowError = false;
            return;
        }

        SetupError = false;
        ShowError = false;
        var profile = new ServerProfile { Name = "Home Assistant", Url = SetupUrl.Trim(), IsDefault = true };
        _servers.AddOrUpdate(profile);
        _settings.Save();
        _tray.RebuildServerMenu();

        IsFirstRun = false;
        ActiveServerName = profile.Name;
        _currentServerId = profile.Id;

        try
        {
            await _webView.InitializeAsync();
            _window.ApplyFullscreenOnLoad();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize after setup");
            ErrorMode = true;
            ShowError = true;
            ErrorText = "WebView2 could not be initialized.\n\nPlease install or update the Microsoft Edge WebView2 Runtime.";
        }
    }
}
