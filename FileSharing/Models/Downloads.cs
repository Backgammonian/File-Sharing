using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;

namespace FileSharing.Models
{
    public class Downloads
    {
        private readonly ConcurrentDictionary<string, Download> _downloads;

        public Downloads()
        {
            _downloads = new ConcurrentDictionary<string, Download>();
        }

        public event EventHandler<EventArgs> DownloadsListUpdated;

        public Download this[string downloadID]
        {
            get => _downloads[downloadID];
            private set => _downloads[downloadID] = value;
        }

        public ObservableCollection<Download> DownloadsList => new ObservableCollection<Download>(_downloads.Values);

        public bool HasDownload(string downloadID)
        {
            return _downloads.ContainsKey(downloadID);
        }

        public void AddDownload(Download download)
        {
            if (!HasDownload(download.Hash))
            {
                if (!download.TryOpenFile())
                {
                    return;
                }

                _downloads.TryAdd(download.ID, download);

                DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveDownload(string downloadID)
        {
            if (HasDownload(downloadID))
            {
                _downloads[downloadID].ShutdownFile();
                _downloads.TryRemove(downloadID, out _);

                DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HasFileWithSamePath(string downloadFilePath, out string downloadID)
        {
            try
            {
                var download = _downloads.Values.Single(download => download.Path == downloadFilePath);
                downloadID = download.ID;

                return true;
            }
            catch (Exception)
            {
                downloadID = "";

                return false;
            }
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
            var downloadsFromServer = _downloads.Values.Where(download => download.ServerID == serverID);
            foreach (var download in downloadsFromServer)
            {
                download.Cancel();
            }
        }
    }
}
