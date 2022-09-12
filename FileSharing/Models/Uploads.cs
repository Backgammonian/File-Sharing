using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FileSharing.Models
{
    public class Uploads
    {
        //<upload ID, upload info>
        private readonly ConcurrentDictionary<string, Upload> _uploads;

        public Uploads()
        {
            _uploads = new ConcurrentDictionary<string, Upload>();
        }

        public event EventHandler<UploadEventArgs>? UploadAdded;
        public event EventHandler<UploadEventArgs>? UploadRemoved;

        public IEnumerable<Upload> UploadsList => _uploads.Values;

        public Upload this[string id]
        {
            get => _uploads[id];
            private set => _uploads[id] = value;
        }

        public bool Has(string uploadID)
        {
            return _uploads.ContainsKey(uploadID);
        }

        public void Add(Upload upload)
        {
            if (_uploads.TryAdd(upload.ID, upload))
            {
                UploadAdded?.Invoke(this, new UploadEventArgs(upload.ID));
            }
        }

        public void Remove(string id)
        {
            if (_uploads.TryRemove(id, out Upload? removedUpload) &&
                removedUpload != null)
            {
                removedUpload.Cancel();
                UploadRemoved?.Invoke(this, new UploadEventArgs(removedUpload.ID));
            }
        }

        public void CancelAllUploadsOfPeer(int peerID)
        {
            var requiredUploads = _uploads.Values.Where(upload => upload.Destination.Id == peerID);

            foreach (var upload in requiredUploads)
            {
                upload.Cancel();
            }
        }

        public List<Upload> GetAllUploadsOfFile(string fileHash)
        {
            return _uploads.Values
                .Where(upload => upload.FileHash == fileHash)
                .ToList();
        }
    }
}
