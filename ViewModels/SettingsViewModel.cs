using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeAssistantDesktop.Models;
using HomeAssistantDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ServerManager _servers;
    private readonly StartupService _startup;
    private readonly WindowService _window;
    private readonly ILogger _log;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _minimizeOnClose;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _fullscreen;

    public ObservableCollection<ServerProfile> Servers { get; } = [];

    [ObservableProperty] private ServerProfile? _editingServer;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editUrl = "";
    [ObservableProperty] private bool _editIsDefault;
    [ObservableProperty] private string _testStatus = "";
    [ObservableProperty] private bool _hasTestStatus;
    [ObservableProperty] private bool _editUrlInvalid;

    public ICommand SaveServerCommand { get; }
    public ICommand AddServerCommand { get; }
    public ICommand DeleteServerCommand { get; }
    public ICommand EditServerCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand TestConnectionEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SetDefaultCommand { get; }

    public event Action? CloseRequested;

    public SettingsViewModel(SettingsService settings, ServerManager servers, StartupService startup,
        WindowService window, ILogger log)
    {
        _settings = settings;
        _servers = servers;
        _startup = startup;
        _window = window;
        _log = log;

        var s = _settings.Settings;
        StartWithWindows = s.StartWithWindows;
        AlwaysOnTop = s.AlwaysOnTop;
        MinimizeOnClose = s.MinimizeOnClose;
        StartMinimized = s.StartMinimized;
        Fullscreen = s.Fullscreen;

        foreach (var sv in s.Servers) Servers.Add(sv);

        SaveServerCommand = new RelayCommand(SaveServer);
        AddServerCommand = new RelayCommand(AddServer);
        DeleteServerCommand = new RelayCommand<ServerProfile>(DeleteServer);
        EditServerCommand = new RelayCommand<ServerProfile>(EditServer);
        TestConnectionCommand = new RelayCommand<ServerProfile>(async s => await TestConnectionAsync(s?.Url ?? ""));
        TestConnectionEditCommand = new RelayCommand(async () => await TestConnectionAsync(EditUrl));
        CancelEditCommand = new RelayCommand(CancelEdit);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        SetDefaultCommand = new RelayCommand<ServerProfile>(s => { if (s is not null) { _servers.SetDefault(s.Id); RefreshServers(); } });
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.Settings.StartWithWindows = value;
        _startup.SetEnabled(value);
        _settings.Save();
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        _settings.Settings.AlwaysOnTop = value;
        _window.SetAlwaysOnTop(value);
        _settings.Save();
    }

    partial void OnMinimizeOnCloseChanged(bool value) { _settings.Settings.MinimizeOnClose = value; _settings.Save(); }
    partial void OnStartMinimizedChanged(bool value) { _settings.Settings.StartMinimized = value; _settings.Save(); }
    partial void OnFullscreenChanged(bool value) { _settings.Settings.Fullscreen = value; _settings.Save(); }

    private void AddServer()
    {
        EditingServer = null;
        IsEditing = true;
        EditName = "New Server";
        EditUrl = "http://192.168.1.100:8123";
        EditIsDefault = Servers.Count == 0;
        EditUrlInvalid = false;
        TestStatus = "";
        HasTestStatus = false;
    }

    private void EditServer(ServerProfile? s)
    {
        if (s is null) return;
        EditingServer = s;
        IsEditing = true;
        EditName = s.Name;
        EditUrl = s.Url;
        EditIsDefault = s.IsDefault;
        EditUrlInvalid = false;
        TestStatus = "";
        HasTestStatus = false;
    }

    private void CancelEdit()
    {
        EditingServer = null;
        IsEditing = false;
        TestStatus = "";
        HasTestStatus = false;
    }

    private void SaveServer()
    {
        if (!Uri.TryCreate(EditUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            EditUrlInvalid = true;
            TestStatus = "URL tidak valid. Gunakan format http://host:port atau https://...";
            HasTestStatus = true;
            _log.LogWarning("Invalid server URL: {Url}", EditUrl);
            return;
        }

        EditUrlInvalid = false;
        if (EditingServer is null)
        {
            var s = new ServerProfile { Name = EditName.Trim(), Url = EditUrl.Trim(), IsDefault = EditIsDefault };
            _servers.AddOrUpdate(s);
            Servers.Add(s);
        }
        else
        {
            var s = EditingServer;
            s.Name = EditName.Trim();
            s.Url = EditUrl.Trim();
            s.IsDefault = EditIsDefault;
            _servers.AddOrUpdate(s);
            RefreshServers();
        }
        EditingServer = null;
        IsEditing = false;
        TestStatus = "";
        HasTestStatus = false;
        _settings.Save();
    }

    private void DeleteServer(ServerProfile? s)
    {
        if (s is null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete server '{s.Name}' ({s.Url})?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        _servers.Delete(s.Id);
        Servers.Remove(s);
        if (EditingServer == s) CancelEdit();
    }

    private async Task TestConnectionAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                TestStatus = "URL tidak valid.";
                HasTestStatus = true;
                return;
            }
            TestStatus = "Testing...";
            HasTestStatus = true;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var baseUri = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            var resp = await client.GetAsync(baseUri);
            TestStatus = resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? $"OK — server reachable ({resp.StatusCode})."
                : $"Reachable but returned {resp.StatusCode}.";
            HasTestStatus = true;
            _log.LogInformation("Test connection to {Url}: {Status}", url, resp.StatusCode);
        }
        catch (Exception ex)
        {
            TestStatus = $"Failed: {ex.Message}";
            HasTestStatus = true;
            _log.LogWarning(ex, "Test connection failed for {Url}", url);
        }
    }

    private void RefreshServers()
    {
        Servers.Clear();
        foreach (var s in _settings.Settings.Servers) Servers.Add(s);
    }
}
