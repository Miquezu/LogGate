using LogGate.Models;
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

    private static Brush? GetBrush(ref Brush? cached, string resourceKey)
    {
        return cached ??= Application.Current?.TryFindResource(resourceKey) as Brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataItem item && parameter != null)
        {
            string? param = parameter.ToString();

            switch (param)
            {
                case "Temp":
                    if (item.Temperature > 37.0)
                        return GetBrush(ref _warningTempBrush, "WarningTempBrush") ?? DependencyProperty.UnsetValue;
                    break;

                case "Alco":
                    if (item.AlcotestResult > 0)
                        return GetBrush(ref _dangerAlcoBrush, "DangerAlcoBrush") ?? DependencyProperty.UnsetValue;
                    break;

                case "Time":
                    if (item.IsLate)
                        return GetBrush(ref _warningLateBrush, "WarningLateBrush") ?? DependencyProperty.UnsetValue;

                    if (item.IsEarlyDeparture)
                        return GetBrush(ref _warningEarlyBrush, "WarningEarlyBrush") ?? DependencyProperty.UnsetValue;
                    break;
            }
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}