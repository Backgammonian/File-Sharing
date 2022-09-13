using System;
using System.Globalization;
using System.Windows.Data;
using LiteNetLib;
using FileSharing.Networking;

namespace FileSharing.Converters
{
    public sealed class ConnectionStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EncryptedPeer peer)
            {
                return peer.ConnectionStatus;
            }

            return ConnectionState.Disconnected;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
