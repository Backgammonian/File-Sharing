using System;

namespace FileSharing.Models
{
    public sealed class UploadEventArgs : EventArgs
    {
        public UploadEventArgs(string uploadID)
        {
            UploadID = uploadID;
        }

        public string UploadID { get; }
    }
}
