using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace FileSharing.Models
{
    public class Downloads : IEnumerable<Download>
    {
        private readonly ConcurrentDictionary<string, Download> _downloads;

        public Downloads()
        {
            _downloads = new ConcurrentDictionary<string, Download>();
        }

        public event EventHandler<EventArgs>? DownloadsListUpdated;

        public Download this[string downloadID]
        {
            get => _downloads[downloadID];
            private set => _downloads[downloadID] = value;
        }

        public IEnumerable<Download> DownloadsList => _downloads.Values;

        public bool HasDownload(string downloadID)
        {
            return _downloads.ContainsKey(downloadID);
        }

        public void AddDownload(Download download)
        {
            if (!HasDownload(download.ID))
            {
                if (!download.TryOpenFile())
                {
                    return;
                }

                if (_downloads.TryAdd(download.ID, download))
                {
                    _downloads[download.ID].FileRemoved += OnFileRemoved;

                    DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnFileRemoved(object? sender, EventArgs e)
        {
            DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveDownload(string downloadID)
        {
            if (HasDownload(downloadID))
            {
                if (_downloads.TryRemove(downloadID, out Download? removedDownload) &&
                    removedDownload != null)
                {
                    removedDownload.ShutdownFile();
                    removedDownload.FileRemoved -= OnFileRemoved;

                    DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool HasFileWithSamePath(string downloadFilePath, out string downloadID)
        {
            foreach (var download in _downloads.Values)
            {
                if (download.Path == downloadFilePath)
                {
                    downloadID = download.ID;
                    return true;
                }
            }

            downloadID = "";
            return false;
        }

        public void ShutdownDownload(string downloadID)
        {
            if (HasDownload(downloadID))
            {
                _downloads[downloadID].ShutdownFile();
            }
        }

        public void ShutdownAllDownloads()
        {
            foreach (var download in _downloads.Values)
            {
                download.ShutdownFile();
            }
        }

        public void CancelAllDownloadsFromServer(int serverID)
        {
            var downloadsFromServer = _downloads.Values.Where(download => download.Server.Id == serverID);
            foreach (var download in downloadsFromServer)
            {
                download.Cancel();
            }
        }

        public IEnumerator<Download> GetEnumerator()
        {
            return _downloads.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_downloads.Values).GetEnumerator();
        }
    }
}
