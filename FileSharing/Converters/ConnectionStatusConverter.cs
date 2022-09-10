using System;
using System.Globalization;
using System.Windows.Data;
using LiteNetLib;

namespace FileSharing.Converters
{
    public class ConnectionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var netPeer = value as NetPeer;
            return netPeer == null ? ConnectionState.Disconnected : netPeer.ConnectionState;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
