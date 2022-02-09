using System;

namespace FileSharing.Models
{
    public class EncryptedPeerEventArgs : EventArgs
    {
        public int PeerID { get; set; }

        public EncryptedPeerEventArgs(int peerID)
        {
            PeerID = peerID;
        }
    }
}
