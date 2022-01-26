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
                _monthAbbreviations[dateTime.Month - 1] + " " +
                dateTime.Year + " " +
                dateTime.Hour + ":" +
                Format(dateTime.Minute) + ":" +
                Format(dateTime.Second);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }

        private readonly string[] _monthAbbreviations = new string[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        private string Format(int number)
        {
            return number.ToString().Length == 1 ? "0" + number : "" + number;
        }
    }
}
