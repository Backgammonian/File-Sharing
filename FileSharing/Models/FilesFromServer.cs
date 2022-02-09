using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FileSharing.Models
{
    public class FilesFromServer
    {
        private readonly ConcurrentDictionary<int, FileInfo> _files;
        private readonly EncryptedPeer _server;

        public FilesFromServer(EncryptedPeer server)
        {
            _files = new ConcurrentDictionary<int, FileInfo>();
            _server = server;
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
                if (_files.TryAdd(i, newFiles[i]))
                {
                    Debug.WriteLine("Added file " + newFiles[i].Name);

                    _files[i].Server = _server;
                }
            }

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}