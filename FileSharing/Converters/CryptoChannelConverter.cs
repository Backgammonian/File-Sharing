using System;
using System.Windows;
using System.Globalization;
using System.Windows.Data;
using System.Net;

namespace FileSharing.Converters
{
    public class CryptoChannelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //TODO
            //var netPeer = (CryptoChannel)value;
            //return netPeer.EndPoint.ToString();
            return (value as IPEndPoint).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
