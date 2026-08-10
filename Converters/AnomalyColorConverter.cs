using LogGate.Models;
using LogGate.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

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
                        if (item.Temperature != null)
                        {
                            string? tempStr = item.Temperature?.ToString()?.Replace(',', '.');
                            if (double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double temp))
                            {
                                if (temp > 37.0)
                                    return new SolidColorBrush(Color.FromRgb(255, 236, 179));
                            }
                        }
                        break;

                    case "Alco":
                        if (item.AlcotestResult > 0)
                            return new SolidColorBrush(Color.FromRgb(255, 205, 210));
                        break;

                    case "Time":
                        if (ScheduleRules.IsLate(item))
                            return Brushes.LightCoral;

                        if (ScheduleRules.IsEarlyDeparture(item))
                            return Brushes.LightYellow;
                        break;
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}