using System;
using System.Windows;
using System.Globalization;
using System.Windows.Data;
using FileSharing.Models;

namespace FileSharing.Converters
{
    public class CryptoPeerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as CryptoPeer).Peer.EndPoint.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}