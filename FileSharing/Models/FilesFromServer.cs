using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using LiteNetLib;

namespace FileSharing.Models
{
    public class FilesFromServer
    {
        private readonly ConcurrentDictionary<int, FileInfo> _files;
        private readonly NetPeer _server;

        public FilesFromServer(NetPeer server)
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

            Debug.WriteLine("--List of files from server " + _server);
            foreach (var file in _files.Values)
            {
                Debug.WriteLine(file.Name);
            }

            ListUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}