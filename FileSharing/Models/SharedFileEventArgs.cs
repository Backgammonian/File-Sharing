using System;

namespace FileSharing.Models
{
    public class SharedFileEventArgs : EventArgs
    {
        public SharedFileEventArgs(string path)
        {
            Path = path;
        }

        public string Path { get; set; }
    }
}
