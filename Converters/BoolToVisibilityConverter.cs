using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace HomeAssistantDesktop;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}
