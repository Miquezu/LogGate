using LogGate.Models;
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
                string param = parameter.ToString();

                // 1. Температура
                if (param == "Temp" && item.Temperature != null)
                {
                    string tempStr = item.Temperature.ToString().Replace(',', '.');
                    if (double.TryParse(tempStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double temp))
                        if (temp > 37.0) return new SolidColorBrush(Color.FromRgb(255, 236, 179));
                }

                // 2. Алкотестер
                if (param == "Alco" && item.AlcotestResult > 0)
                    return new SolidColorBrush(Color.FromRgb(255, 205, 210));

                // 3. Опоздания (Вход после 08:00)
                if (param == "Late" && item.EventTime.HasValue && !string.IsNullOrEmpty(item.Direction))
                {
                    if (item.Direction.StartsWith("Вх", StringComparison.OrdinalIgnoreCase) && item.EventTime.Value.TimeOfDay > new TimeSpan(8, 0, 0))
                        return new SolidColorBrush(Color.FromRgb(255, 205, 210)); // Красный
                }

                // 4. Ушли рано (Выход до 17:00)
                if (param == "Early" && item.EventTime.HasValue && !string.IsNullOrEmpty(item.Direction))
                {
                    if (item.Direction.StartsWith("Вых", StringComparison.OrdinalIgnoreCase) && item.EventTime.Value.TimeOfDay < new TimeSpan(17, 0, 0))
                        return new SolidColorBrush(Color.FromRgb(255, 236, 179)); // Желтый
                }
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}