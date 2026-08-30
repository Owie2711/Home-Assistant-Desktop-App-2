using System.Windows;
using HomeAssistantDesktop.ViewModels;

namespace HomeAssistantDesktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var vm = new SettingsViewModel(App.Settings, App.Servers, App.AutoStart, App.Window, App.Log);
        vm.CloseRequested += () => Close();
        DataContext = vm;
    }
}
