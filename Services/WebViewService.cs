using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace HomeAssistantDesktop.Services;

public enum ConnectionState
{
    Unknown,
    Connecting,
    Connected,
    Offline
}

public sealed class WebViewService
{
    private readonly SettingsService _settings;
    private readonly ServerManager _servers;
    private readonly ILogger _log;

    private WebView2? _webView;
    private CoreWebView2Environment? _environment;
    private readonly Dictionary<string, CoreWebView2Environment> _envCache = [];
    private CancellationTokenSource? _connCts;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private bool _disposed;

    public ConnectionState State { get; private set; } = ConnectionState.Unknown;
    public event Action<ConnectionState>? ConnectionStateChanged;

    public WebViewService(SettingsService settings, ServerManager servers, ILogger log)
    {
        _settings = settings;
        _servers = servers;
        _log = log;
    }

    public void Attach(WebView2 webView) => _webView = webView;

    public async Task InitializeAsync()
    {
        if (_webView is null) return;
        var server = _servers.GetActive();
        if (server is null)
        {
            _log.LogWarning("No server configured");
            return;
        }

        try
        {
            var userData = _servers.ProfileDirectoryFor(server);
            Directory.CreateDirectory(userData);
            if (!_envCache.TryGetValue(server.Id, out var env))
            {
                env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
                _envCache[server.Id] = env;
            }
            _environment = env;
            await _webView.EnsureCoreWebView2Async(_environment);
            _log.LogInformation("WebView2 initialized for server {Name} ({Url})", server.Name, server.Url);

            _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.SourceChanged += (_, _) => { };
            _webView.ZoomFactorChanged += OnZoomFactorChanged;

            ApplyZoom();
            await NavigateToServerAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WebView2 initialization failed");
            State = ConnectionState.Offline;
            ConnectionStateChanged?.Invoke(State);
            throw;
        }
    }

    public async Task NavigateToServerAsync()
    {
        var server = _servers.GetActive();
        if (server is null || _webView?.CoreWebView2 is null) return;
        _log.LogInformation("Navigating to {Name} ({Url})", server.Name, server.Url);
        SetState(ConnectionState.Connecting);
        await _webView.Dispatcher.InvokeAsync(() => _webView.CoreWebView2.Navigate(server.Url));
    }

    public void NavigateBack() => _webView?.CoreWebView2?.GoBack();
    public void NavigateForward() => _webView?.CoreWebView2?.GoForward();
    public void Reload() => _webView?.CoreWebView2?.Reload();
    public void Stop() => _webView?.CoreWebView2?.Stop();

    private void ApplyZoom()
    {
        if (_webView is not null)
            _webView.ZoomFactor = _settings.Settings.ZoomFactor;
    }

    public void SetZoom(double factor)
    {
        if (_webView is not null)
        {
            _webView.ZoomFactor = factor;
            _settings.Settings.ZoomFactor = factor;
            _settings.Save();
        }
    }

    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        if (_webView is not null)
        {
            _settings.Settings.ZoomFactor = _webView.ZoomFactor;
            _settings.Save();
        }
    }

    public async Task SwitchServerAsync(string id)
    {
        _servers.SetActive(id);
        await RecreateEnvironmentForActiveAsync();
    }

    private async Task RecreateEnvironmentForActiveAsync()
    {
        if (_webView is null) return;
        var server = _servers.GetActive()!;
        var userData = _servers.ProfileDirectoryFor(server);
        Directory.CreateDirectory(userData);

        if (!_envCache.TryGetValue(server.Id, out var env))
        {
            env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            _envCache[server.Id] = env;
        }
        _environment = env;

        await _webView.EnsureCoreWebView2Async(_environment);
        var core = _webView.CoreWebView2!;
        core.NavigationStarting -= OnNavigationStarting;
        core.NavigationCompleted -= OnNavigationCompleted;
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        _webView.ZoomFactorChanged -= OnZoomFactorChanged;
        _webView.ZoomFactorChanged += OnZoomFactorChanged;
        ApplyZoom();
        await NavigateToServerAsync();
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        SetState(ConnectionState.Connecting);
        _connCts?.Cancel();
        _connCts = new CancellationTokenSource();
        _ = ProbeConnectionAsync(_connCts.Token);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            SetState(ConnectionState.Connected);
            _log.LogInformation("Navigation completed successfully");
        }
        else
        {
            _log.LogWarning("Navigation failed: {Code}", e.WebErrorStatus);
            SetState(ConnectionState.Offline);
        }
    }

    private async Task ProbeConnectionAsync(CancellationToken token)
    {
        var server = _servers.GetActive();
        if (server is null) return;
        try
        {
            var uri = new Uri(server.Url);
            var baseUri = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            var resp = await _httpClient.GetAsync(baseUri, token);
            if (!token.IsCancellationRequested)
                SetState(resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? ConnectionState.Connected
                    : ConnectionState.Offline);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Connection probe failed");
            if (!token.IsCancellationRequested) SetState(ConnectionState.Offline);
        }
    }

    private void SetState(ConnectionState state)
    {
        if (State == state) return;
        State = state;
        ConnectionStateChanged?.Invoke(state);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connCts?.Cancel();
        _connCts?.Dispose();
        _envCache.Clear();
        _webView?.Dispose();
    }
}
