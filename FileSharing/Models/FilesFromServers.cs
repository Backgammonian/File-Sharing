using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            List = new List<FileInfo>();
        }

        public event EventHandler<EventArgs>? FilesUpdated;

        public List<FileInfo> List { get; }

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

                    Debug.WriteLine("(AddServer) server: " + server.EndPoint);
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

            Debug.WriteLine("AvailableFiles");
            foreach (var file in List)
            {
                Debug.WriteLine(file.Name + " " + file.Server.EndPoint);
            }

            FilesUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
