using System;
using System.Collections.Generic;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class MissingSegmentsEventArgs : EventArgs
    {
        public MissingSegmentsEventArgs(string downloadID, string fileHash, List<long> numbersOfMissingSegments, EncryptedPeer server)
        {
            DownloadID = downloadID;
            FileHash = fileHash;
            NumbersOfMissingSegments = numbersOfMissingSegments;
            Server = server;
        }

        public string DownloadID { get; }
        public string FileHash { get; }
        public List<long> NumbersOfMissingSegments { get; }
        public EncryptedPeer Server { get; }
    }
}
