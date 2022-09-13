using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class SharedFile : ObservableObject
    {
        private FileStream? _stream;
        private bool _isHashCalculated;
        private string _hash = string.Empty;

        public SharedFile(long index, string path)
        {
            Index = index;
            FilePath = path;
            IsHashCalculated = false;
            Hash = string.Empty;
        }

        public long Index { get; }
        public string FilePath { get; }
        public string Name { get; private set; } = string.Empty;
        public long Size { get; private set; }
        public long NumberOfSegments { get; private set; }
       
        public bool IsHashCalculated
        {
            get => _isHashCalculated;
            private set => SetProperty(ref _isHashCalculated, value);
        }

        public string Hash
        {
            get => _hash;
            private set => SetProperty(ref _hash, value);
        }

        public bool TryOpenStream()
        {
            try
            {
                _stream = File.OpenRead(FilePath);
                Name = Path.GetFileName(FilePath);
                Size = _stream.Length;
                NumberOfSegments = Size / Constants.FileSegmentSize + (Size % Constants.FileSegmentSize != 0 ? 1 : 0);

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        public void CloseStream()
        {
            if (_stream != null)
            {
                _stream.Close();
            }
        }

        public async Task<bool> TryComputeHashOfFile()
        {
            if (IsHashCalculated)
            {
                return true;
            }

            var fileHash = await CryptographyModule.ComputeFileHash(FilePath);
            if (fileHash != string.Empty)
            {
                Hash = fileHash;
                IsHashCalculated = true;

                return true;
            }

            return false;
        }

        public bool TryReadSegment(long numberOfSegment, out byte[] fileSegment)
        {
            if (TryReadSegmentInternal(numberOfSegment, out byte[] segment))
            {
                fileSegment = segment;

                return true;
            }
            else
            {
                fileSegment = Array.Empty<byte>();

                return false;
            }
        }

        private bool TryReadSegmentInternal(long numberOfSegment, out byte[] segment)
        {
            segment = Array.Empty<byte>();

            if (_stream == null)
            {
                return false;
            }

            try
            {
                var buffer = new byte[Constants.FileSegmentSize];
                _stream.Seek(numberOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
                var readBytes = _stream.Read(buffer, 0, buffer.Length);

                if (readBytes == buffer.Length)
                {
                    segment = buffer;
                }
                else
                {
                    var specialBuffer = new byte[readBytes];
                    Buffer.BlockCopy(buffer, 0, specialBuffer, 0, readBytes);
                    segment = specialBuffer;
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}