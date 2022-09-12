﻿using System;
using System.Globalization;
using System.Windows.Data;
using System.Net;
using FileSharing.Networking;

namespace FileSharing.Converters
{
    public class EncryptedPeerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var peer = value as EncryptedPeer;
            return peer == null ? new IPEndPoint(0, 0).ToString() : peer.Peer.EndPoint;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
