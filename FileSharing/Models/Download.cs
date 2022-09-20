using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;
using FileSharing.Networking.Utils;

namespace FileSharing.Models
{
    public sealed class Download : ObservableObject
    {
        private readonly SpeedCounter _downloadSpeedCounter;
        private FileStream? _stream;
        private bool _isDownloaded;
        private bool _isCancelled;
        private HashVerificationStatus _hashVerificationStatus;
        private string _calculatedHash = string.Empty;
        private long _numberOfReceivedSegments;

        public Download(SharedFileInfo availableFile, EncryptedPeer server, string path)
        {
            ID = RandomGenerator.GetRandomString(30);
            OriginalName = availableFile.Name;
            Name = Path.GetFileName(path);
            FilePath = path;
            Size = availableFile.Size;
            NumberOfSegments = availableFile.NumberOfSegments;
            Hash = availableFile.Hash;
            Server = server;
            IsDownloaded = false;
            IsCancelled = false;
            HashVerificationStatus = HashVerificationStatus.None;
            StartTime = DateTime.Now;
            CalculatedHash = CryptographyModule.DefaultFileHash;
            NumberOfReceivedSegments = 0;

            _downloadSpeedCounter = new SpeedCounter();
            _downloadSpeedCounter.Updated += OnDownloadSpeedCounterUpdated;
        }

        public event EventHandler<EventArgs>? FileRemoved;
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
        public double DownloadSpeed => _downloadSpeedCounter.Speed;
        public double AverageSpeed => _downloadSpeedCounter.AverageSpeed;
        public long BytesDownloaded => _downloadSpeedCounter.Bytes;
        public decimal Progress => NumberOfReceivedSegments / Convert.ToDecimal(NumberOfSegments);

        public long NumberOfReceivedSegments
        {
            get => _numberOfReceivedSegments;
            private set
            {
                SetProperty(ref _numberOfReceivedSegments, value);
                OnPropertyChanged(nameof(Progress));
            }
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

        public HashVerificationStatus HashVerificationStatus
        {
            get => _hashVerificationStatus;
            private set => SetProperty(ref _hashVerificationStatus, value);
        }

        public string CalculatedHash
        {
            get => _calculatedHash;
            private set => SetProperty(ref _calculatedHash, value);
        }

        private void UpdateParameters()
        {
            OnPropertyChanged(nameof(DownloadSpeed));
            OnPropertyChanged(nameof(AverageSpeed));
            OnPropertyChanged(nameof(BytesDownloaded));
            OnPropertyChanged(nameof(Progress));
        }

        private void OnDownloadSpeedCounterUpdated(object? sender, EventArgs e)
        {
            UpdateParameters();
        }

        private void AddReceivedBytes(long numOfSegment, byte[] segment)
        {
            _stream?.Seek(numOfSegment * Constants.FileSegmentSize, SeekOrigin.Begin);
            _stream?.Write(segment);
            _downloadSpeedCounter.AddBytes(segment.Length);
            NumberOfReceivedSegments += 1;
            if (NumberOfReceivedSegments == NumberOfSegments)
            {
                FinishDownload();
            }

            UpdateParameters();
        }

        private void FinishDownload()
        {
            Finished?.Invoke(this, new DownloadFinishedEventArgs(Name));
            IsDownloaded = true;
            ShutdownFile();

            Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File '{Name}' - bytes downloaded: {BytesDownloaded} of {Size}, " +
                $"segments received: {NumberOfReceivedSegments} of {NumberOfSegments}");

            var verifyHashTask = new Task(() => VerifyHash());
            verifyHashTask.Start();
        }

        private void VerifyHash()
        {
            if (HashVerificationStatus != HashVerificationStatus.None ||
                !IsDownloaded)
            {
                return;
            }

            HashVerificationStatus = HashVerificationStatus.Started;
            CalculatedHash = CryptographyModule.ComputeFileHash(FilePath);

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

        public bool TryOpenFile()
        {
            try
            {
                _stream = new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, Constants.FileSegmentSize);

                return true;
            }
            catch (Exception)
            {
                _downloadSpeedCounter.Stop();

                return false;
            }
        }

        public void ShutdownFile()
        {
            _downloadSpeedCounter.Stop();

            if (_stream != null)
            {
                _stream.Close();
                _stream.Dispose();
            }

            UpdateParameters();
        }

        public DownloadingFileWriteStatus TryWrite(uint receivedCrc, long numOfSegment, byte[] segment)
        {
            if (!IsActive)
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File {Name} is already downloaded/cancelled!");

                return DownloadingFileWriteStatus.DoNothing;
            }

            if (numOfSegment < 0 ||
                numOfSegment >= NumberOfSegments)
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) File {Name}: wrong number of incoming file segment - {numOfSegment}");

                return DownloadingFileWriteStatus.DoNothing;
            }

            if (receivedCrc != CRC32.Compute(segment))
            {
                Debug.WriteLine($"(DownloadFile_AddReceivedBytes) CRC of file segment {Name} is wrong");

                return DownloadingFileWriteStatus.Failure;
            }

            try
            {
                AddReceivedBytes(numOfSegment, segment);

                return DownloadingFileWriteStatus.Success;
            }
            catch (Exception)
            {
                return DownloadingFileWriteStatus.Failure;
            }
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
    }
}