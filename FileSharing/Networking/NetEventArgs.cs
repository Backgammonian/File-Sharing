using System;
using LiteNetLib.Utils;

namespace FileSharing.Networking
{
    public class NetEventArgs : EventArgs
    {
        public NetEventArgs(EncryptedPeer cryptoPeer, NetDataReader message)
        {
            Message = message;
            CryptoPeer = cryptoPeer;
        }

        public NetDataReader Message { get; }
        public EncryptedPeer CryptoPeer { get; }
    }
}
