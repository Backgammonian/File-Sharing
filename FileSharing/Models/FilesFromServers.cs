using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace FileSharing.Models
{
    public class FilesFromServers
    {
        //server ID (from NetPeer object), list of files
        private readonly ConcurrentDictionary<int, FilesFromServer> _filesDictionary;

        public FilesFromServers()
        {
            _filesDictionary = new ConcurrentDictionary<int, FilesFromServer>();
            AvailableFiles = new ObservableCollection<FileInfo>();
        }

        public event EventHandler<EventArgs> FilesUpdated;

        public ObservableCollection<FileInfo> AvailableFiles { get; }

        public FilesFromServer this[int serverID]
        {
            get => _filesDictionary[serverID];
            private set => _filesDictionary[serverID] = value;
        }

        public bool HasServer(int serverID)
        {
            return _filesDictionary.ContainsKey(serverID);
        }

        public void AddServer(int serverID)
        {
            if (!HasServer(serverID))
            {
                _filesDictionary.TryAdd(serverID, new FilesFromServer());
                _filesDictionary[serverID].ListUpdated += OnFileListUpdated;

                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveServer(int serverID)
        {
            if (HasServer(serverID))
            {
                _filesDictionary[serverID].ListUpdated -= OnFileListUpdated;
                _filesDictionary.TryRemove(serverID, out _);

                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFileListUpdated(object sender, EventArgs e)
        {
            AvailableFiles.Clear();
            foreach (var serverFilesList in _filesDictionary.Values)
            {
                var files = serverFilesList.Files;
                foreach (var file in files)
                {
                    AvailableFiles.Add(file);
                }
            }

            FilesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
