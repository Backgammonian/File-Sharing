using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FileSharing.Models
{
    public class FilesFromServer
    {
        private readonly ConcurrentDictionary<int, FileInfo> _files;

        public FilesFromServer()
        {
            _files = new ConcurrentDictionary<int, FileInfo>();
        }

        public event EventHandler<EventArgs>? ListUpdated;

        public IEnumerable<FileInfo> Files => _files.Values;

        public void Clear()
        {
            _files.Clear();

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateWith(List<FileInfo> newFiles)
        {
            _files.Clear();
            newFiles.Sort((x, y) => x.Name.CompareTo(y.Name));
            for (int i = 0; i < newFiles.Count; i++)
            {
                _files.TryAdd(i, newFiles[i]);
            }

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}