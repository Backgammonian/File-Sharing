using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace FileSharing.Models
{
    public sealed class Downloads
    {
        private readonly ConcurrentDictionary<string, Download> _downloads;

        public Downloads()
        {
            _downloads = new ConcurrentDictionary<string, Download>();
        }

        public event EventHandler<MissingSegmentsEventArgs>? MissingSegmentsRequested;
        public event EventHandler<EventArgs>? DownloadsListUpdated;
        public event EventHandler<DownloadFinishedEventArgs>? DownloadFinished;

        public IEnumerable<Download> DownloadsList =>
            _downloads.Values.OrderBy(download => download.StartTime);

        public Download? Get(string downloadID)
        {
            return HasDownload(downloadID) ? _downloads[downloadID] : null;
        }

        public bool HasDownload(string downloadID)
        {
            return _downloads.ContainsKey(downloadID);
        }

        public bool TryAddDownload(Download download)
        {
            if (HasDownload(download.ID))
            {
                Debug.WriteLine($"(TryAddDownload) Already have download with ID {download.ID}");

                return false;
            }

            if (!download.TryOpenFile())
            {
                Debug.WriteLine($"(TryAddDownload) Can't create file for download with ID {download.ID}");

                return false;
            }

            if (_downloads.TryAdd(download.ID, download))
            {
                _downloads[download.ID].Finished += OnDownloadFinished;
                _downloads[download.ID].FileRemoved += OnFileRemoved;
                _downloads[download.ID].MissingSegmentsRequested += OnMissingFileSegmentsRequested;
                DownloadsListUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }

            Debug.WriteLine($"(TryAddDownload) Can't add download {download.ID} into collection");

            return false;
        }

        public void RemoveDownload(string downloadID)
        {
            if (HasDownload(downloadID) &&
                _downloads.TryRemove(downloadID, out Download? removedDownload) &&
                removedDownload != null)
            {
                removedDownload.ShutdownFile();
                removedDownload.Finished -= OnDownloadFinished;
                removedDownload.FileRemoved -= OnFileRemoved;
                removedDownload.MissingSegmentsRequested -= OnMissingFileSegmentsRequested;
                DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnFileRemoved(object? sender, EventArgs e)
        {
            DownloadsListUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnMissingFileSegmentsRequested(object? sender, MissingSegmentsEventArgs e)
        {
            MissingSegmentsRequested?.Invoke(this, e);
        }

        private void OnDownloadFinished(object? sender, DownloadFinishedEventArgs e)
        {
            DownloadFinished?.Invoke(this, e);
        }

        public bool HasDownloadWithSamePath(string downloadFilePath, out string downloadID)
        {
            downloadID = string.Empty;

            try
            {
                var download = _downloads.Values.First(target =>
                    target.FilePath == downloadFilePath && target.IsActive);
                downloadID = download.ID;

                return true;
            }
            catch (Exception)
            {
                Debug.WriteLine($"(HasDownloadWithSamePath) No match with file {downloadFilePath}");

                return false;
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
    }
}
