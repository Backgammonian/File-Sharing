using System;
using System.Diagnostics;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public class Upload : ObservableObject
    {
        private readonly bool[] _fileSegmentsCheck;
        private long _numberOfAckedSegments;
        private bool _isFinished;
        private bool _isCancelled;
        private DateTime _startTime;
        private DateTime _finishTime;
        private long _bytesSent;
        private long _resendedFileSegments;

        public Upload(string id, string fileName, long fileSize, string fileHash, EncryptedPeer destination, long numberOfSegments)
        {
            ID = id;
            FileName = fileName;
            FileSize = fileSize;
            FileHash = fileHash;
            Destination = destination;
            NumberOfSegments = numberOfSegments;
            NumberOfAckedSegments = Constants.ChannelsCount - 1;
            IsFinished = false;
            IsCancelled = false;
            StartTime = DateTime.Now;
            BytesSent = 0;

            _fileSegmentsCheck = new bool[NumberOfSegments];
            for (long i = 0; i < _fileSegmentsCheck.LongLength; i++)
            {
                _fileSegmentsCheck[i] = false;
            }

            ResendedFileSegments = 0;
        }

        public string ID { get; }
        public string FileName { get; }
        public long FileSize { get; }
        public string FileHash { get; }
        public EncryptedPeer Destination { get; }
        public long NumberOfSegments { get; }
        public bool IsActive => !IsCancelled && !IsFinished;
        public decimal Progress => NumberOfAckedSegments / Convert.ToDecimal(NumberOfSegments);
        public double AverageSpeed => BytesSent / (DateTime.Now - StartTime).TotalSeconds;

        public long NumberOfAckedSegments
        {
            get => _numberOfAckedSegments;
            private set
            {
                SetProperty(ref _numberOfAckedSegments, value);
                OnPropertyChanged(nameof(Progress));
            }
        }
    
        public bool IsFinished
        {
            get => _isFinished;
            private set => SetProperty(ref _isFinished, value);
        }
        
        public bool IsCancelled
        {
            get => _isCancelled;
            private set => SetProperty(ref _isCancelled, value);
        }
        
        public DateTime StartTime
        {
            get => _startTime;
            private set => SetProperty(ref _startTime, value);
        }

        public DateTime FinishTime
        {
            get => _finishTime;
            private set => SetProperty(ref _finishTime, value);
        }

        public long BytesSent
        {
            get => _bytesSent;
            private set
            {
                SetProperty(ref _bytesSent, value);
                OnPropertyChanged(nameof(AverageSpeed));
            }
        }

        public long ResendedFileSegments
        {
            get => _resendedFileSegments;
            private set => SetProperty(ref _resendedFileSegments, value);
        }

        public void AddAck(long numOfSegment)
        {
            if (!IsActive)
            {
                return;
            }

            if (numOfSegment < 0 ||
                numOfSegment >= NumberOfSegments)
            {
                Debug.WriteLine($"(Upload_AddAck) File {FileName}: wrong number of incoming file segment!");

                return;
            }

            if (_fileSegmentsCheck[numOfSegment])
            {
                Debug.WriteLine($"(Upload_AddAck) File {FileName}: already sent segment {numOfSegment}!");

                return;
            }

            _fileSegmentsCheck[numOfSegment] = true;
            NumberOfAckedSegments += 1;
            BytesSent += Constants.FileSegmentSize;

            if (NumberOfAckedSegments == NumberOfSegments)
            {
                IsFinished = true;
                FinishTime = DateTime.Now;
            }
        }

        public void Cancel()
        {
            if (IsFinished)
            {
                return;
            }

            IsCancelled = true;
        }

        public void AddResendedSegment()
        {
            ResendedFileSegments += 1;
        }
    }
}