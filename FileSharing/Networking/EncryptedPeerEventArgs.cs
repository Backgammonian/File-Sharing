using System;

namespace FileSharing.Networking
{
    public class EncryptedPeerEventArgs : EventArgs
    {
        public EncryptedPeerEventArgs(int peerID)
        {
            PeerID = peerID;
        }

        public int PeerID { get; }
    }
}
