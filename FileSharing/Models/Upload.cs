using System;
using System.Diagnostics;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using FileSharing.Networking;

namespace FileSharing.Models
{
    public sealed class Upload : ObservableObject
    {
        private long _numberOfAckedSegments;
        private bool _isFinished;
        private bool _isCancelled;
        private DateTime _startTime;
        private DateTime _finishTime;
        private long _resendedFileSegments;

        public Upload(string id, string fileName, long fileSize, string fileHash, EncryptedPeer destination, long numberOfSegments)
        {
            ID = id;
            FileName = fileName;
            FileSize = fileSize;
            FileHash = fileHash;
            Destination = destination;
            NumberOfSegments = numberOfSegments;
            NumberOfAckedSegments = 0;
            IsFinished = false;
            IsCancelled = false;
            StartTime = DateTime.Now;
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
        public double AverageSpeed => (NumberOfAckedSegments - NumberOfSegments) / (DateTime.Now - StartTime).TotalSeconds;

        public long NumberOfAckedSegments
        {
            get => _numberOfAckedSegments;
            private set
            {
                SetProperty(ref _numberOfAckedSegments, value);
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(AverageSpeed));
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

        public long ResendedFileSegments
        {
            get => _resendedFileSegments;
            private set => SetProperty(ref _resendedFileSegments, value);
        }

        private void Finish()
        {
            IsFinished = true;
            FinishTime = DateTime.Now;
        }

        public UploadingFileAckStatus AddAck(long numOfSegment)
        {
            if (IsFinished)
            {
                Debug.WriteLine($"(Upload_AddAck) Upload of file {FileName} is finished already!");

                return UploadingFileAckStatus.DoNothing;
            }

            if (!IsActive)
            {
                Debug.WriteLine($"(Upload_AddAck) Upload of file {FileName} is no longer active");

                return UploadingFileAckStatus.DoNothing;
            }

            if (numOfSegment < 0 ||
                numOfSegment >= NumberOfSegments)
            {
                Debug.WriteLine($"(Upload_AddAck) File {FileName}: wrong number of incoming file segment");

                return UploadingFileAckStatus.DoNothing;
            }

            Debug.WriteLine($"(Upload_AddAck) Receiving ACK for file {FileName}: #{numOfSegment}");

            NumberOfAckedSegments += 1;
            if (NumberOfSegments == NumberOfAckedSegments)
            {
                Finish();
            }

            return UploadingFileAckStatus.Success;
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