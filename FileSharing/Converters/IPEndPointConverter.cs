using System;
using System.Globalization;
using System.Windows.Data;
using System.Net;
using FileSharing.Networking;

namespace FileSharing.Converters
{
    public sealed class IPEndPointConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IPEndPoint endPoint)
            {
                return endPoint.ToString();
            }
            else
            if (value is EncryptedPeer peer)
            {
                return peer.ToString();
            }

            return new IPEndPoint(0, 0).ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
