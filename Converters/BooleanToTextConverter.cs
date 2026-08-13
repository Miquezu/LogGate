using System.Globalization;
using System.Windows.Data;

namespace LogGate.Converters
{
    public class BooleanToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPersonal)
                return isPersonal ? "Сотрудник" : "Отдел";
            return "Отдел";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}