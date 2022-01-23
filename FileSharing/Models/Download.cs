using System;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Windows.Threading;
using Microsoft.Toolkit.Mvvm.ComponentModel;
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
        private int _serverID;
        private IPEndPoint _serverAddress;
        private bool _isDownloaded;
        private bool _isCancelled;
        private long _bytesDownloaded;
        private double _downloadSpeed;

        public Download(string name, long size, string hash, int serverID, IPEndPoint serverAddress, string path)
        {
            ID = RandomGenerator.GetRandomString(20);
            Name = name;
            Path = path;
            Size = size;
            Hash = hash;
            ServerID = serverID;
            ServerAddress = serverAddress;
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;

            InitializeTimer();
        }

        public Download(FileInfo file, string path)
        {
            ID = RandomGenerator.GetRandomString(20);
            Name = file.Name;
            Path = path;
            Size = file.Size;
            Hash = file.Hash;
            ServerID = file.ServerID;
            ServerAddress = file.ServerAddress;
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;

            InitializeTimer();
        }

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

        public int ServerID
        {
            get => _serverID;
            private set => SetProperty(ref _serverID, value);
        }

        public IPEndPoint ServerAddress
        {
            get => _serverAddress;
            private set => SetProperty(ref _serverAddress, value);
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

        public double Progress => Convert.ToDouble(BytesDownloaded) / Convert.ToDouble(Size);

        public bool TryOpenFile()
        {
            try
            {
                _speedCounter.Start();
                _stream = File.OpenWrite(Path);

                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);

                return false;
            }
        }

        public void ShutdownFile()
        {
            DownloadSpeed = 0;
            _speedCounter.Stop();
            _stream.Close();
            _stream.Dispose();
        }

        public void Write(uint numOfSegment, byte[] segment)
        {
            _stream.Seek(numOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
            _stream.Write(segment, 0, segment.Length);
            AddReceivedBytes(segment.Length);
        }

        private void AddReceivedBytes(int amountOfBytes)
        {
            if (IsCancelled || IsDownloaded)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File " + Name + " is already downloaded or cancelled!");
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
                Debug.WriteLine("(DownloadFile_Cancel) File " + Name + " is already downloaded or cancelled!");
                return;
            }

            Debug.WriteLine("(DownloadFile_Cancel) " + Name + ": download has been cancelled!");

            IsCancelled = true;
            ShutdownFile();

            try
            {
                File.Delete(Path);
            }
            catch (Exception)
            {
            }
        }
    }
}
