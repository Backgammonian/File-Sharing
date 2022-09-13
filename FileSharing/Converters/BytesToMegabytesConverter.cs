using System;
using System.Globalization;
using System.Windows.Data;
using Extensions;

namespace FileSharing.Converters
{
    public sealed class BytesToMegabytesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((long)value).GetSizeSuffix();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
