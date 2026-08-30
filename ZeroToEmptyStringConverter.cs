using System;
using System.Globalization;
using System.Windows.Data;

namespace AccountingApp
{
    public class ZeroToEmptyStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            if (value is int intValue && intValue == 0)
                return string.Empty;

            if (value is decimal decValue && decValue == 0)
                return string.Empty;

            if (value is double dblValue && dblValue == 0)
                return string.Empty;

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(value as string))
            {
                if (targetType == typeof(int)) return 0;
                if (targetType == typeof(decimal)) return 0m;
                if (targetType == typeof(double)) return 0.0;
            }
            return System.Convert.ChangeType(value, targetType);
        }
    }
}