﻿using System;

namespace FileSharing.Models
{
    public sealed class DownloadFinishedEventArgs : EventArgs
    {
        public DownloadFinishedEventArgs(string fileName)
        {
            DownloadedFileName = fileName;
        }

        public string DownloadedFileName { get; }
    }
}
