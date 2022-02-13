using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
            List = new List<SharedFileInfo>();
        }

        public event EventHandler<EventArgs>? FilesUpdated;

        public List<SharedFileInfo> List { get; }

        public FilesFromServer this[int serverID]
        {
            get => _filesFromServers[serverID];
            private set => _filesFromServers[serverID] = value;
        }

        public bool HasServer(int serverID)
        {
            return _filesFromServers.ContainsKey(serverID);
        }

        public void AddServer(EncryptedPeer server)
        {
            if (!HasServer(server.Peer.Id))
            {
                if (_filesFromServers.TryAdd(server.Peer.Id, new FilesFromServer(server)))
                {
                    _filesFromServers[server.Peer.Id].ListUpdated += OnFileListUpdated;

                    FilesUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void RemoveServer(int serverID)
        {
            if (HasServer(serverID))
            {
                _filesFromServers[serverID].Clear();

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
            List.Clear();
            foreach (var serverFilesList in _filesFromServers.Values)
            {
                var files = serverFilesList.Files;
                foreach (var file in files)
                {
                    List.Add(file);
                }
            }

            FilesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
