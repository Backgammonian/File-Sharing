using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using LiteNetLib;

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

        public void AddServer(NetPeer server)
        {
            if (!HasServer(server.Id))
            {
                if (_filesFromServers.TryAdd(server.Id, new FilesFromServer(server)))
                {
                    _filesFromServers[server.Id].ListUpdated += OnFileListUpdated;

                    FilesUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void RemoveServer(int serverID)
        {
            if (HasServer(serverID))
            {
                if (_filesFromServers.TryRemove(serverID, out FilesFromServer? removedList) &&
                    removedList != null)
                {
                    removedList.ListUpdated -= OnFileListUpdated;

                    FilesUpdated?.Invoke(this, EventArgs.Empty);
                }
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
