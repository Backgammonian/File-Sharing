using System;
using LiteNetLib.Utils;
using FileSharing.Models;

namespace FileSharing.Networking
{
    public class NetEventArgs : EventArgs
    {
        public NetDataReader Message { get; set; }
        public EncryptedPeer CryptoPeer { get; set; }

        public NetEventArgs(EncryptedPeer cryptoPeer, NetDataReader message)
        {
            Message = message;
            CryptoPeer = cryptoPeer;
        }
    }
}
