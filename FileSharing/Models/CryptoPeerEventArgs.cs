using System;

namespace FileSharing.Models
{
    public class CryptoPeerEventArgs : EventArgs
    {
        public int PeerID { get; set; }

        public CryptoPeerEventArgs(int peerID)
        {
            PeerID = peerID;
        }
    }
}
