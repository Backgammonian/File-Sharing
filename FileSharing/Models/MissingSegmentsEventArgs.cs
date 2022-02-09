using System;
using System.Collections.Generic;

namespace FileSharing.Models
{
    public class MissingSegmentsEventArgs : EventArgs
    {
        public MissingSegmentsEventArgs(string downloadID, string fileHash, List<long> numbersOfMissingSegments, EncryptedPeer server)
        {
            DownloadID = downloadID;
            FileHash = fileHash;
            NumbersOfMissingSegments = numbersOfMissingSegments;
            Server = server;
        }

        public string DownloadID { get; set; }
        public string FileHash { get; set; }
        public List<long> NumbersOfMissingSegments { get; set; }
        public EncryptedPeer Server { get; set; }
    }
}
