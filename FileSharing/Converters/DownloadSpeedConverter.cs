using System;
using System.Globalization;
using System.Windows.Data;
using Extensions;

namespace FileSharing.Converters
{
    public sealed class DownloadSpeedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var downloadSpeed = System.Convert.ToInt64((double)value);
            return downloadSpeed.GetSizeSuffix() + "/s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
