using LogGate.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LogGate.Converters
{
    public class AnomalyColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DataItem item && parameter != null)
            {
                string? param = parameter.ToString();

                switch (param)
                {
                    case "Temp":
                        if (item.Temperature.HasValue && item.Temperature.Value > 37.0)
                            return Application.Current.FindResource("WarningTempBrush");
                        break;

                    case "Alco":
                        if (item.AlcotestResult > 0)
                            return Application.Current.FindResource("DangerAlcoBrush");
                        break;

                    case "Time":
                        if (item.IsLate)
                            return Application.Current.FindResource("WarningLateBrush");

                        if (item.IsEarlyDeparture)
                            return Application.Current.FindResource("WarningEarlyBrush");
                        break;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}