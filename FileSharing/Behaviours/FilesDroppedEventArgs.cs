using System;

namespace Behaviours
{
    public class FilesDroppedEventArgs : EventArgs
    {
        public FilesDroppedEventArgs(string[] filesPaths)
        {
            FilesPath = filesPaths;
        }

        public string[] FilesPath { get; }
    }
}
