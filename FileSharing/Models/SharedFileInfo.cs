using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class SharedFileInfo : ObservableObject
    {
        private EncryptedPeer? _server;
        private string _name = string.Empty;
        private long _size;
        private long _numberOfSegments;
        private string _hash = string.Empty;

        public SharedFileInfo(SharedFile sharedFile)
        {
            if (sharedFile == null)
            {
                return;
            }

            Name = sharedFile.Name;
            Size = sharedFile.Size;
            NumberOfSegments = sharedFile.NumberOfSegments;
            Hash = sharedFile.Hash;
            Server = null;
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public long Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public long NumberOfSegments
        {
            get => _numberOfSegments;
            set => SetProperty(ref _numberOfSegments, value);
        }

        public string Hash
        {
            get => _hash;
            set => SetProperty(ref _hash, value);
        }

        public EncryptedPeer? Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }
    }
}