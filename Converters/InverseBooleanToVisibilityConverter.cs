using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogGate.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b
            ? (b ? Visibility.Collapsed : Visibility.Visible)
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility v && v != Visibility.Visible;
}

