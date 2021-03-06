using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Utils;
using FileSharing.Modules;

namespace FileSharing.Models
{
    public class Download : ObservableObject
    {
        private FileStream _stream;
        private decimal _progress;
        private bool _isDownloaded;
        private bool _isCancelled;
        private bool _isHashVerificationStarted;
        private bool _isHashVerificationFailed;
        private bool _isHashVerificationResultPositive;
        private bool _isHashVerificationResultNegative;
        private double _averageSpeed;
        private readonly Stopwatch _stopwatch;
        private long _oldAmountOfDownloadedBytes, _newAmountOfDownloadedBytes;
        private DateTime _oldDownloadTimeStamp, _newDownloadTimeStamp;
        private long _bytesDownloaded;
        private readonly DispatcherTimer _downloadSpeedCounter;
        private double _downloadSpeed;
        private Queue<double> _downloadSpeedValues;
        private const double _interval = 100.0;
        private bool[] _fileSegmentsCheck;
        private long[] _incomingSegmentsChannelsStatistic;
        private readonly DispatcherTimer _missingSegmentsTimer;

        public Download(SharedFileInfo availableFile, EncryptedPeer server, string path)
        {
            ID = RandomGenerator.GetRandomString(20);
            OriginalName = availableFile.Name;
            Name = System.IO.Path.GetFileName(path);
            Path = path;
            Size = availableFile.Size;
            NumberOfSegments = availableFile.NumberOfSegments;
            Hash = availableFile.Hash;
            Server = server;
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;

            _fileSegmentsCheck = new bool[NumberOfSegments];
            for (long i = 0; i < _fileSegmentsCheck.LongLength; i++)
            {
                _fileSegmentsCheck[i] = false;
            }

            _stopwatch = new Stopwatch();
            _downloadSpeedValues = new Queue<double>();
            _downloadSpeedCounter = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _downloadSpeedCounter.Interval = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(_interval));
            _downloadSpeedCounter.Tick += (sender, e) =>
            {
                _oldAmountOfDownloadedBytes = _newAmountOfDownloadedBytes;
                _newAmountOfDownloadedBytes = BytesDownloaded;

                _oldDownloadTimeStamp = _newDownloadTimeStamp;
                _newDownloadTimeStamp = DateTime.Now;

                var value = (_newAmountOfDownloadedBytes - _oldAmountOfDownloadedBytes) / (_newDownloadTimeStamp - _oldDownloadTimeStamp).TotalSeconds;
                _downloadSpeedValues.Enqueue(value);

                if (_downloadSpeedValues.Count > 20)
                {
                    _downloadSpeedValues.Dequeue();
                }

                var seconds = _stopwatch.Elapsed.Seconds > 0 ? _stopwatch.Elapsed.Seconds : 0.01;
                AverageSpeed = BytesDownloaded / Convert.ToDouble(seconds);

                DownloadSpeed = CalculateMovingAverageDownloadSpeed();
            };

            _incomingSegmentsChannelsStatistic = new long[Constants.ChannelsCount];

            _missingSegmentsTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _missingSegmentsTimer.Interval = new TimeSpan(0, 0, 0, 0, Constants.DisconnectionTimeout / 2);
            _missingSegmentsTimer.Tick += (s, e) =>
            {
                if (!IsActive)
                {
                    _missingSegmentsTimer.Stop();
                    return;
                }

                var numbersOfMissingSegments = new List<long>();
                for (long i = 0; i < _fileSegmentsCheck.Length; i++)
                {
                    if (!_fileSegmentsCheck[i])
                    {
                        numbersOfMissingSegments.Add(i);
                    }
                }

                Debug.WriteLine("(Download_MissingSegmentsTimer) Requesting " + numbersOfMissingSegments.Count + " segments");

                MissingSegmentsRequested?.Invoke(this, new MissingSegmentsEventArgs(ID, Hash, numbersOfMissingSegments, Server));
            };
            _missingSegmentsTimer.Start();
        }

        public event EventHandler<EventArgs>? FileRemoved;
        public event EventHandler<MissingSegmentsEventArgs>? MissingSegmentsRequested;

        public string ID { get; }
        public string OriginalName { get; }
        public string Name { get; }
        public string Path { get; }
        public long Size { get; }
        public long NumberOfSegments { get; }
        public string Hash { get; }
        public EncryptedPeer Server { get; }
        public bool IsActive => !IsCancelled && !IsDownloaded;

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

        public bool IsHashVerificationStarted
        {
            get => _isHashVerificationStarted;
            private set => SetProperty(ref _isHashVerificationStarted, value);
        }

        public bool IsHashVerificationFailed
        {
            get => _isHashVerificationFailed;
            private set => SetProperty(ref _isHashVerificationFailed, value);
        }

        public bool IsHashVerificationResultPositive
        {
            get => _isHashVerificationResultPositive;
            private set
            {
                SetProperty(ref _isHashVerificationResultPositive, value);

                _isHashVerificationResultNegative = !value;
                OnPropertyChanged(nameof(IsHashVerificationResultNegative));
            }
        }

        public bool IsHashVerificationResultNegative
        {
            get => _isHashVerificationResultNegative;
            private set
            {
                SetProperty(ref _isHashVerificationResultNegative, value);

                _isHashVerificationResultPositive = !value;
                OnPropertyChanged(nameof(IsHashVerificationResultPositive));
            }
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            private set
            {
                SetProperty(ref _bytesDownloaded, value);

                Progress = BytesDownloaded / Convert.ToDecimal(Size);
            }
        }

        public double DownloadSpeed
        {
            get => _downloadSpeed;
            private set => SetProperty(ref _downloadSpeed, value);
        }

        public double AverageSpeed
        {
            get => _averageSpeed;
            private set => SetProperty(ref _averageSpeed, value);
        }

        public decimal Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }
    
        private double CalculateMovingAverageDownloadSpeed()
        {
            var result = 0.0;
            foreach (var value in _downloadSpeedValues)
            {
                result += value;
            }

            return result / _downloadSpeedValues.Count;
        }

        public bool TryOpenFile()
        {
            try
            {
                _downloadSpeedCounter.Start();
                _stopwatch.Start();
                _stream = File.OpenWrite(Path);

                return true;
            }
            catch (Exception)
            {
                _downloadSpeedCounter.Stop();
                _stopwatch.Stop();

                return false;
            }
        }

        public void ShutdownFile()
        {
            DownloadSpeed = 0;
            _downloadSpeedCounter.Stop();
            _missingSegmentsTimer.Stop();
            _stopwatch.Stop();
            _stream.Close();
            _stream.Dispose();
        }

        public bool TryWrite(long numOfSegment, byte[] segment, byte channel)
        {
            if (channel >= 0 &&
                channel < Constants.ChannelsCount)
            {
                _incomingSegmentsChannelsStatistic[channel] += 1;
            }

            if (!IsActive)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File " + Name + " is already downloaded/cancelled!");

                return false;
            }

            if (numOfSegment < 0 ||
                numOfSegment >= NumberOfSegments)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File " + Name + ": wrong number of incoming file segment!");

                return false;
            }

            if (_fileSegmentsCheck[numOfSegment])
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File " + Name + " already have segment " + numOfSegment + "!");

                return false;
            }

            try
            {
                AddReceivedBytes(numOfSegment, segment);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AddReceivedBytes(long numOfSegment, byte[] segment)
        {
            _missingSegmentsTimer.Stop();
            _missingSegmentsTimer.Start();

            _stream.Seek(numOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
            _stream.Write(segment, 0, segment.Length);

            _fileSegmentsCheck[numOfSegment] = true;

            BytesDownloaded += segment.Length;
            if (BytesDownloaded == Size)
            {
                Debug.WriteLine("(DownloadFile_AddReceivedBytes) File '{0}': All bytes downloaded! {1} of {2}", Name, BytesDownloaded, Size);

                IsDownloaded = true;
                ShutdownFile();

                Debug.WriteLine("(DownloadFile_Statistics) Number of segments: " + NumberOfSegments);
                Debug.WriteLine("(DownloadFile_Statistics) Used Channels:");
                for (int i = 0; i < _incomingSegmentsChannelsStatistic.Length; i++)
                {
                    Debug.WriteLine("\t Channel " + i + " : " + (_incomingSegmentsChannelsStatistic[i] / Convert.ToDouble(NumberOfSegments) * 100.0) + "%");
                }
            }
        }

        public void Cancel()
        {
            if (!IsActive)
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

        public void ProhibitHashVerification()
        {
            IsHashVerificationFailed = true;
        }

        public void VerifyHash()
        {
            if (IsHashVerificationStarted ||
                IsHashVerificationFailed)
            {
                return;
            }

            Debug.WriteLine("(Download) Hash verification started!");
            
            IsHashVerificationStarted = true;
            if (TryComputeHashOfFile(out bool result))
            {
                if (result)
                {
                    IsHashVerificationResultPositive = true;

                    Debug.WriteLine("(Download) Hash verification result is positive!");
                }
                else
                {
                    IsHashVerificationResultNegative = true;

                    Debug.WriteLine("(Download) Hash verification result is negative!");
                }
            }
        }

        private bool TryComputeHashOfFile(out bool result)
        {
            if (CryptographyModule.TryComputeFileHash(Path, out string calculatedHash))
            {
                result = calculatedHash == Hash;

                Debug.WriteLine("(TryComputeHashOfFile) Calculated hash: " + calculatedHash);
                Debug.WriteLine("(TryComputeHashOfFile) Result: " + result);

                return true;
            }

            result = false;

            return false;
        }
    }
}