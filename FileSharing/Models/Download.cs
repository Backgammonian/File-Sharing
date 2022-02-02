using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using LiteNetLib;
using FileSharing.Utils;

namespace FileSharing.Models
{
    public class Download : ObservableObject
    {
        private FileStream _stream;
        private DispatcherTimer _speedCounter;
        private long _oldAmountOfBytes, _newAmountOfBytes;
        private DateTime _oldTimeStamp, _newTimeStamp;
        private string _id;
        private string _name;
        private string _path;
        private long _size;
        private string _hash;
        private NetPeer _server;
        private bool _isDownloaded;
        private bool _isCancelled;
        private long _bytesDownloaded;
        private double _downloadSpeed;
        private bool _isCorrupted;

        public Download(string name, long size, string hash, NetPeer? server, string folder)
        {
            ID = RandomGenerator.GetRandomString(20);
            Name = name;
            Path = folder + "\\" + name;
            Size = size;
            Hash = hash;
            if (server != null)
            {
                Server = server;
            }
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;

            InitializeTimer();
        }

        public Download(FileInfo file, string folder)
        {
            ID = RandomGenerator.GetRandomString(20);
            Name = file.Name;
            Path = folder + "\\" + file.Name;
            Size = file.Size;
            Hash = file.Hash;
            if (file.Server != null)
            {
                Server = file.Server;
            }
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;

            InitializeTimer();
        }

        public event EventHandler<EventArgs>? FileRemoved;

        private void InitializeTimer()
        {
            _speedCounter = new DispatcherTimer();
            _speedCounter.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            _speedCounter.Tick += (sender, e) =>
            {
                _oldAmountOfBytes = _newAmountOfBytes;
                _newAmountOfBytes = BytesDownloaded;

                _oldTimeStamp = _newTimeStamp;
                _newTimeStamp = DateTime.Now;

                DownloadSpeed = (_newAmountOfBytes - _oldAmountOfBytes) / (_newTimeStamp - _oldTimeStamp).TotalSeconds;
            };
        }

        public string ID
        {
            get => _id;
            private set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            private set => SetProperty(ref _name, value);
        }

        public string Path
        {
            get => _path;
            private set => SetProperty(ref _path, value);
        }

        public long Size
        {
            get => _size;
            private set => SetProperty(ref _size, value);
        }

        public string Hash
        {
            get => _hash;
            private set => SetProperty(ref _hash, value);
        }

        public NetPeer Server
        {
            get => _server;
            private set => SetProperty(ref _server, value);
        }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            private set => SetProperty(ref _isDownloaded, value);
        }

        public bool IsCancelled
        {
            get => _isCancelled;
            private set => SetProperty(ref _isCancelled, value);
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            private set
            {
                SetProperty(ref _bytesDownloaded, value);
                OnPropertyChanged(nameof(Progress));
            }
        }

        public double DownloadSpeed
        {
            get => _downloadSpeed;
            private set => SetProperty(ref _downloadSpeed, value);
        }

        public bool IsCorrupted
        {
            get => _isCorrupted;
            private set => SetProperty(ref _isCorrupted, value);
        }

        public decimal Progress => BytesDownloaded / Convert.ToDecimal(Size);

        public bool TryOpenFile()
        {
            if (IsCorrupted)
            {
                return false;
            }
            
            try
            {
                _speedCounter.Start();
                _stream = File.OpenWrite(Path);

                return true;
            }
            catch (Exception)
            {
                _speedCounter.Stop();
                IsCorrupted = true;

                return false;
            }
        }

        public void ShutdownFile()
        {
            if (IsCorrupted)
            {
                return;
            }

            try
            {
                DownloadSpeed = 0;
                _speedCounter.Stop();
                _stream.Close();
                _stream.Dispose();
            }
            catch (Exception)
            {
                IsCorrupted = true;
            }
        }

        public bool TryWrite(long numOfSegment, byte[] segment)
        {
            if (IsCorrupted)
            {
                return false;
            }

            try
            {
                _stream.Seek(numOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
                _stream.Write(segment, 0, segment.Length);

                AddReceivedBytes(segment.Length);

                return true;
            }
            catch (Exception)
            {
                IsCorrupted = true;

                return false;
            }
        }

        private void AddReceivedBytes(long amountOfBytes)
        {
            if (IsCancelled || IsDownloaded)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File " + Name + " is already downloaded/cancelled!");
                return;
            }

            BytesDownloaded += amountOfBytes;
            if (BytesDownloaded >= Size)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) {0}: All bytes downloaded! {1} of {2}", Name, BytesDownloaded, Size);

                IsDownloaded = true;
                ShutdownFile();
            }
        }

        public void Cancel()
        {
            if (IsCancelled || IsDownloaded)
            {
                Debug.WriteLine("(DownloadFile_Cancel) File " + Name + " is already downloaded/cancelled!");
                return;
            }

            Debug.WriteLine("(DownloadFile_Cancel) " + Name + ": download has been cancelled!");

            IsCancelled = true;
            ShutdownFile();

            try
            {
                File.Delete(Path);

                FileRemoved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
            }
        }
    }
}
