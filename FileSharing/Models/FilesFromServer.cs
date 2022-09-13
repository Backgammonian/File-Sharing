using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class FilesFromServer
    {
        private readonly ConcurrentDictionary<int, SharedFileInfo> _files;
        private readonly EncryptedPeer _server;

        public FilesFromServer(EncryptedPeer server)
        {
            _files = new ConcurrentDictionary<int, SharedFileInfo>();
            _server = server;
        }

        public event EventHandler<EventArgs>? ListUpdated;

        public IEnumerable<SharedFileInfo> Files => _files.Values;

        public void Clear()
        {
            _files.Clear();

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateWith(List<SharedFileInfo> newFiles)
        {
            _files.Clear();
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