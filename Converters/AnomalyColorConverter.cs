using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LogGate.Converters;

public class AnomalyColorConverter : IValueConverter
{
    private static Brush? _warningTempBrush;
    private static Brush? _dangerAlcoBrush;
    private static Brush? _warningLateBrush;
    private static Brush? _warningEarlyBrush;

    private static Brush? GetBrush(ref Brush? cached, string resourceKey) =>
        cached ??= Application.Current?.TryFindResource(resourceKey) as Brush;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DataItem item || parameter is null)
            return DependencyProperty.UnsetValue;

        return parameter.ToString() switch
        {
            "Temp" when item.Temperature > 37.0 =>
                GetBrush(ref _warningTempBrush, "WarningTempBrush") ?? DependencyProperty.UnsetValue,
            "Alco" when item.AlcotestResult > 0 =>
                GetBrush(ref _dangerAlcoBrush, "DangerAlcoBrush") ?? DependencyProperty.UnsetValue,
            "Time" when item.IsLate =>
                GetBrush(ref _warningLateBrush, "WarningLateBrush") ?? DependencyProperty.UnsetValue,
            "Time" when item.IsEarlyDeparture =>
                GetBrush(ref _warningEarlyBrush, "WarningEarlyBrush") ?? DependencyProperty.UnsetValue,
            _ => DependencyProperty.UnsetValue
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
}