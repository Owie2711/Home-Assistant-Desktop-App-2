using System.Windows;
using System.Windows.Input;
using HomeAssistantDesktop.ViewModels;

namespace HomeAssistantDesktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        vm.CloseRequested += () => Close();
        DataContext = vm;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}
