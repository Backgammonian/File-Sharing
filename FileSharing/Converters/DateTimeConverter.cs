using System;
using System.Windows;
using System.Globalization;
using System.Windows.Data;

namespace FileSharing.Converters
{
    public class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dateTime = (DateTime)value;
            return dateTime.Day + " " +
                _abbreviationsOfMonths[dateTime.Month - 1] + " " +
                dateTime.Year + " " +
                dateTime.Hour + ":" +
                Format(dateTime.Minute) + ":" +
                Format(dateTime.Second);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }

        private readonly string[] _abbreviationsOfMonths = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private string Format(int number)
        {
            var s = "" + number;
            if (s.Length == 1)
            {
                s = "0" + s[0];
            }
            return s;
        }
    }
}
