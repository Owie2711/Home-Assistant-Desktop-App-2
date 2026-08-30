using System.IO;
using System.Windows;
using System.Windows.Input;
using HomeAssistantDesktop.ViewModels;
using Microsoft.Extensions.Logging;

namespace HomeAssistantDesktop.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(App.WebView, App.Window, App.Settings, App.Servers, App.Tray, App.Log);
        DataContext = _vm;
        App.WebView.Attach(WebView);
        App.Window.Attach(this);
        App.Window.ApplySavedState();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(iconPath)) Icon = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(iconPath));

        App.Tray.OpenRequested += () => ShowAndActivate();
        App.Tray.SettingsRequested += () => ShowSettings();
        App.Tray.ExitRequested += () => System.Windows.Application.Current.Shutdown();
        _vm.SettingsRequested += ShowSettings;
        SizeChanged += MainWindow_SizeChanged;
    }

    private System.Windows.Threading.DispatcherTimer? _saveTimer;
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Minimized) return;
        _saveTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
        _saveTimer.Stop();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); App.Window.SaveState(); };
        _saveTimer.Start();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();
    }

    private void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = false;
        Topmost = App.Settings.Settings.AlwaysOnTop;
    }

    private async void ShowSettings()
    {
        var win = new SettingsWindow();
        win.Owner = this;
        win.ShowDialog();
        await _vm.OnSettingsClosedAsync();
    }

    private void Retry_Click(object sender, RoutedEventArgs e) => _vm.RetryCommand.Execute(null);
    private void ErrorSettings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (App.Settings.Settings.MinimizeOnClose)
        {
            e.Cancel = true;
            App.Window.SaveState();
            Hide();
            return;
        }
        App.Window.SaveState();
        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && App.Settings.Settings.MinimizeOnClose)
            Hide();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            _vm.FullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }
}
