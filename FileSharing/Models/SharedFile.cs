using System;
using System.IO;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Modules;

namespace FileSharing.Models
{
    public class SharedFile : ObservableObject
    {
        private readonly FileStream _stream;
        private bool _isHashCalculated;
        private string _hash;

        public SharedFile(long index, string path)
        {
            Index = index;
            Path = path;
            _stream = File.OpenRead(Path);
            Name = System.IO.Path.GetFileName(path);
            Size = _stream.Length;

            var div = Size / Constants.FileSegmentSize;
            var mod = Size % Constants.FileSegmentSize;
            NumberOfSegments = div + (mod != 0 ? 1 : 0);

            IsHashCalculated = false;
            Hash = string.Empty;
        }

        public long Index { get; }
        public string Name { get; }
        public long Size { get; }
        public long NumberOfSegments { get; }
        public string Path { get; }
       
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

        public void CloseStream()
        {
            _stream.Close();
        }

        public bool TryComputeHashOfFile()
        {
            if (IsHashCalculated)
            {
                return true;
            }

            if (CryptographyModule.TryComputeFileHash(Path, out string fileHash))
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
                segment = Array.Empty<byte>();

                return false;
            }
        }
    }
}