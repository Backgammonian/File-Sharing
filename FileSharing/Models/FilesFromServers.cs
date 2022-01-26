using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace FileSharing.Models
{
    public class FilesFromServers
    {
        //server ID (from NetPeer object), list of files
        private readonly ConcurrentDictionary<int, FilesFromServer> _filesFromServers;

        public FilesFromServers()
        {
            _filesFromServers = new ConcurrentDictionary<int, FilesFromServer>();
            AvailableFiles = new ObservableCollection<FileInfo>();
        }

        public event EventHandler<EventArgs>? FilesUpdated;

        public ObservableCollection<FileInfo> AvailableFiles { get; }

        public FilesFromServer this[int serverID]
        {
            get => _filesFromServers[serverID];
            private set => _filesFromServers[serverID] = value;
        }

        public bool HasServer(int serverID)
        {
            return _filesFromServers.ContainsKey(serverID);
        }

        public void AddServer(int serverID)
        {
            if (!HasServer(serverID))
            {
                _filesFromServers.TryAdd(serverID, new FilesFromServer());
                _filesFromServers[serverID].ListUpdated += OnFileListUpdated;

                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveServer(int serverID)
        {
            if (HasServer(serverID))
            {
                _filesFromServers[serverID].ListUpdated -= OnFileListUpdated;
                _filesFromServers.TryRemove(serverID, out _);

                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFileListUpdated(object? sender, EventArgs e)
        {
            AvailableFiles.Clear();
            foreach (var serverFilesList in _filesFromServers.Values)
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
