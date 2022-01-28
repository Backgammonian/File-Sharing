using System;

namespace FileSharing.Models
{
    public class UploadEventArgs : EventArgs
    {
        public string UploadID { get; set; }

        public UploadEventArgs(string uploadID)
        {
            UploadID = uploadID;
        }
    }
}
