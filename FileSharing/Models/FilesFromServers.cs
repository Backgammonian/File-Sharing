using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class FilesFromServers
    {
        //server ID (from NetPeer object), list of files
        private readonly ConcurrentDictionary<int, FilesFromServer> _filesFromServers;
        private readonly List<SharedFileInfo> _filesList;

        public FilesFromServers()
        {
            _filesFromServers = new ConcurrentDictionary<int, FilesFromServer>();
            _filesList = new List<SharedFileInfo>();
        }

        public event EventHandler<EventArgs>? FilesUpdated;

        public IReadOnlyCollection<SharedFileInfo> List => _filesList;

        public FilesFromServer? GetServerByID(int serverID)
        {
            return HasServer(serverID) ? _filesFromServers[serverID] : null;
        }

        public bool HasServer(int serverID)
        {
            return _filesFromServers.ContainsKey(serverID);
        }

        public void AddServer(EncryptedPeer server)
        {
            if (!HasServer(server.Id) &&
                _filesFromServers.TryAdd(server.Id, new FilesFromServer(server)))
            {
                _filesFromServers[server.Id].ListUpdated += OnFileListUpdated;
                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveServer(int serverID)
        {
            if (HasServer(serverID) &&
                _filesFromServers.TryRemove(serverID, out FilesFromServer? removedList) &&
                removedList != null)
            {
                removedList.Clear();
                removedList.ListUpdated -= OnFileListUpdated;
                FilesUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFileListUpdated(object? sender, EventArgs e)
        {
            _filesList.Clear();
            foreach (var serverFilesList in _filesFromServers.Values)
            {
                var files = serverFilesList.Files;
                foreach (var file in files)
                {
                    _filesList.Add(file);
                }
            }
            FilesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
