using System;

namespace FileSharing.Networking
{
    public sealed class EncryptedPeerEventArgs : EventArgs
    {
        public EncryptedPeerEventArgs(int peerID)
        {
            PeerID = peerID;
        }

        public int PeerID { get; }
    }
}
