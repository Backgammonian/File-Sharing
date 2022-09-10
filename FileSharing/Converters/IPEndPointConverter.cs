using System;
using System.Globalization;
using System.Windows.Data;
using System.Net;
using LiteNetLib;

namespace FileSharing.Converters
{
    public class IPEndPointConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var netPeer = value as NetPeer;
            return netPeer == null ? new IPEndPoint(0, 0).ToString() : netPeer.EndPoint;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
