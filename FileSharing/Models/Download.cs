﻿using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;
using FileSharing.Networking.Utils;

namespace FileSharing.Models
{
    public sealed class Download : ObservableObject
    {
        private const double _interval = 100.0;
        private static readonly TimeSpan _downloadSpeedCounterInterval = TimeSpan.FromMilliseconds(Convert.ToInt32(_interval));
        private static readonly TimeSpan _missingSegmentsTimerInterval = TimeSpan.FromMilliseconds(Constants.DisconnectionTimeout / 2);

        private readonly Stopwatch _stopwatch;
        private readonly Queue<double> _downloadSpeedValues;
        private readonly bool[] _fileSegmentsCheck;
        private readonly long[] _incomingSegmentsChannelsStatistic;
        private readonly DispatcherTimer _downloadSpeedCounter;
        private readonly DispatcherTimer _missingSegmentsTimer;
        private FileStream? _stream;
        private decimal _progress;
        private bool _isDownloaded;
        private bool _isCancelled;
        private HashVerificationStatus _hashVerificationStatus;
        private double _averageSpeed;
        private long _oldAmountOfDownloadedBytes, _newAmountOfDownloadedBytes;
        private DateTime _oldDownloadTimeStamp, _newDownloadTimeStamp;
        private long _bytesDownloaded;
        private double _downloadSpeed;
        private string _calculatedHash = string.Empty;

        public Download(SharedFileInfo availableFile, EncryptedPeer server, string path)
        {
            ID = RandomGenerator.GetRandomString(20);
            OriginalName = availableFile.Name;
            Name = Path.GetFileName(path);
            FilePath = path;
            Size = availableFile.Size;
            NumberOfSegments = availableFile.NumberOfSegments;
            Hash = availableFile.Hash;
            Server = server;
            IsDownloaded = false;
            IsCancelled = false;
            BytesDownloaded = 0;
            DownloadSpeed = 0;
            HashVerificationStatus = HashVerificationStatus.None;
            StartTime = DateTime.Now;
            CalculatedHash = string.Empty;

            _fileSegmentsCheck = new bool[NumberOfSegments];
            for (long i = 0; i < _fileSegmentsCheck.LongLength; i++)
            {
                _fileSegmentsCheck[i] = false;
            }

            _stopwatch = new Stopwatch();
            _downloadSpeedValues = new Queue<double>();
            _incomingSegmentsChannelsStatistic = new long[Constants.ChannelsCount];

            _downloadSpeedCounter = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _downloadSpeedCounter.Interval = _downloadSpeedCounterInterval;
            _downloadSpeedCounter.Tick += OnDownloadSpeedCounterTick;

            _missingSegmentsTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _missingSegmentsTimer.Interval = _missingSegmentsTimerInterval;
            _missingSegmentsTimer.Tick += OnMissingSegmentsTimerTick;
            _missingSegmentsTimer.Start();
        }

        public event EventHandler<EventArgs>? FileRemoved;
        public event AsyncEventHandler<MissingSegmentsEventArgs>? MissingSegmentsRequested;
        public event EventHandler<DownloadFinishedEventArgs>? Finished;

        public string ID { get; }
        public string OriginalName { get; }
        public string Name { get; }
        public string FilePath { get; }
        public long Size { get; }
        public long NumberOfSegments { get; }
        public string Hash { get; }
        public EncryptedPeer Server { get; }
        public DateTime StartTime { get; }
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

        public HashVerificationStatus HashVerificationStatus
        {
            get => _hashVerificationStatus;
            private set => SetProperty(ref _hashVerificationStatus, value);
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

        public string CalculatedHash
        {
            get => _calculatedHash;
            private set => SetProperty(ref _calculatedHash, value);
        }

        private void OnDownloadSpeedCounterTick(object? sender, EventArgs e)
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

            DownloadSpeed = _downloadSpeedValues.CalculateAverageValue();
        }

        //async void is kind of a bad practice but I don't know better solution for this
        private async void OnMissingSegmentsTimerTick(object? sender, EventArgs e)
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

            Debug.WriteLine($"(Download_MissingSegmentsTimer) Requesting {numbersOfMissingSegments.Count} segments");

            if (MissingSegmentsRequested != null)
            {
                await MissingSegmentsRequested.Invoke(this, new MissingSegmentsEventArgs(ID, Hash, numbersOfMissingSegments, Server));
            }
        }

        public bool TryOpenFile()
        {
            try
            {
                _downloadSpeedCounter.Start();
                _stopwatch.Start();
                _stream = File.OpenWrite(FilePath);

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

            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
            }
        }

        public async Task<bool> TryWrite(long numOfSegment, byte[] segment, byte channel)
        {
            if (channel >= 0 &&
                channel < Constants.ChannelsCount)
            {
                _incomingSegmentsChannelsStatistic[channel] += 1;
            }

            if (!IsActive)
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File {Name} is already downloaded/cancelled!");

                return false;
            }

            if (numOfSegment < 0 ||
                numOfSegment >= NumberOfSegments)
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File {Name} wrong number of incoming file segment!");

                return false;
            }

            if (_fileSegmentsCheck[numOfSegment])
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File {Name} already have segment {numOfSegment}!");

                return false;
            }

            try
            {
                await AddReceivedBytes(numOfSegment, segment);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task AddReceivedBytes(long numOfSegment, byte[] segment)
        {
            _missingSegmentsTimer.Stop();
            _missingSegmentsTimer.Start();

            if (_stream == null)
            {
                Debug.WriteLine($"(AddReceivedBytes) File stream {Name} is null!");

                return;
            }

            _stream.Seek(numOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
            await _stream.WriteAsync(segment, 0, segment.Length);

            _fileSegmentsCheck[numOfSegment] = true;

            BytesDownloaded += segment.Length;
            if (BytesDownloaded == Size)
            {
                await FinishDownload();
            }
        }

        private async Task FinishDownload()
        {
            Finished?.Invoke(this, new DownloadFinishedEventArgs(Name));
            IsDownloaded = true;
            ShutdownFile();

            Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File '{Name}': All bytes downloaded! {BytesDownloaded} of {Size}");
            Debug.WriteLine($"(DownloadFile_Statistics) Number of segments: {NumberOfSegments}");
            Debug.WriteLine("(DownloadFile_Statistics) Used Channels:");
            for (int i = 0; i < _incomingSegmentsChannelsStatistic.Length; i++)
            {
                var percentage = _incomingSegmentsChannelsStatistic[i] / Convert.ToDouble(NumberOfSegments) * 100.0;
                Debug.WriteLine($"\t Channel {i}: {percentage}%");
            }

            await VerifyHash();
        }

        public void Cancel()
        {
            if (!IsActive)
            {
                Debug.WriteLine($"(DownloadFile_Cancel) File {Name} is already downloaded/cancelled!");

                return;
            }

            Debug.WriteLine($"(DownloadFile_Cancel) {Name}: download has been cancelled!");

            IsCancelled = true;
            ShutdownFile();

            try
            {
                File.Delete(FilePath);
                FileRemoved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
            }
        }

        public async Task VerifyHash()
        {
            if (HashVerificationStatus != HashVerificationStatus.None ||
                !IsDownloaded)
            {
                return;
            }

            HashVerificationStatus = HashVerificationStatus.Started;
            CalculatedHash = await CryptographyModule.ComputeFileHash(FilePath);

            Debug.WriteLine($"(VerifyHash) Calculated hash: {CalculatedHash}");
            Debug.WriteLine($"(VerifyHash) Original hash: {Hash}");

            if (CalculatedHash == CryptographyModule.DefaultFileHash)
            {
                HashVerificationStatus = HashVerificationStatus.Failed;

                Debug.WriteLine("(VerifyHash) Hash verification has failed.");
            }
            else
            if (CalculatedHash == Hash)
            {
                HashVerificationStatus = HashVerificationStatus.Positive;

                Debug.WriteLine("(VerifyHash) Hash verification result is positive!");
            }
            else
            {
                HashVerificationStatus = HashVerificationStatus.Negative;

                Debug.WriteLine("(VerifyHash) Hash verification result is negative.");
            }
        }
    }
}