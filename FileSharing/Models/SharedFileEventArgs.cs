using System;

namespace FileSharing.Models
{
    public class SharedFileEventArgs : EventArgs
    {
        public string Path { get; set; }

        public SharedFileEventArgs(string path)
        {
            Path = path;
        }
    }
}
