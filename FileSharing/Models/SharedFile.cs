using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Utils;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public class SharedFile : ObservableObject
    {
        private FileStream _stream;
        private string _name;
        private long _size;
        private string _path;
        private long _numberOfSegments;
        private bool _isHashCalculated;
        private string _hash;
        private bool _isCorrupted;

        public SharedFile(long index, string name, long size, string path)
        {
            Index = index;
            Name = name;
            Size = size;
            Path = path;
            IsHashCalculated = false;
            Hash = string.Empty;
            IsCorrupted = false;
        }

        public event EventHandler<EventArgs>? HashCalculated;

        public long Index { get; }

        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        public long Size
        {
            get => _size;
            private set
            {
                SetProperty(ref _size, value);

                var div = _size / Constants.FileSegmentSize;
                var mod = _size % Constants.FileSegmentSize;

                NumberOfSegments = div + (mod != 0 ? 1 : 0);
            }
        }

        public string Path
        {
            get => _path;
            private set => SetProperty(ref _path, value);
        }

        public long NumberOfSegments
        {
            get => _numberOfSegments;
            private set => SetProperty(ref _numberOfSegments, value);
        }
        
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

        public bool IsCorrupted
        {
            get => _isCorrupted;
            private set => SetProperty(ref _isCorrupted, value);
        }

        public void OpenStream()
        {
            if (IsCorrupted)
            {
                return;
            }

            try
            {
                _stream = File.OpenRead(Path);
            }
            catch (Exception)
            {
                IsCorrupted = true;
            }
        }

        public void CloseStream()
        {
            if (IsCorrupted)
            {
                return;
            }

            try
            {
                _stream.Close();
                _stream.Dispose();
            }
            catch (Exception)
            {
                IsCorrupted = true;
            }
        }

        public void ComputeHashOfFile()
        {
            if (IsHashCalculated)
            {
                return;
            }

            using (var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, 10 * 1024 * 1024))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                Hash = BitConverter.ToString(hash).ToLower().Replace("-", "");
                IsHashCalculated = true;

                HashCalculated?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool TryReadSegment(long numberOfSegment, string uploadID, out SimpleWriter writer)
        {
            writer = new SimpleWriter();

            if (IsCorrupted)
            {
                return false;
            }

            try
            {
                writer.Put((byte)NetMessageTypes.FileSegment);
                writer.Put(uploadID);
                writer.Put(numberOfSegment);

                var buffer = new byte[Constants.FileSegmentSize];
                _stream.Seek(numberOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
                var readBytes = _stream.Read(buffer, 0, buffer.Length);

                if (readBytes == buffer.Length)
                {
                    var crc = CRC32.Compute(buffer);

                    writer.Put(crc);
                    writer.Put(buffer.Length);
                    writer.Put(buffer);
                }
                else
                {
                    var segment = new byte[readBytes];
                    Buffer.BlockCopy(buffer, 0, segment, 0, readBytes);

                    var crc = CRC32.Compute(segment);

                    writer.Put(crc);
                    writer.Put(segment.Length);
                    writer.Put(segment);
                }

                return true;
            }
            catch (Exception)
            {
                IsCorrupted = true;

                return false;
            }
        }
    }
}
