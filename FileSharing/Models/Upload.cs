using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace FileSharing.Models
{
    public class Upload : ObservableObject
    {
        private string _id;
        private string _fileName;
        private long _fileSize;
        private string _fileHash;
        private CryptoPeer _destination;
        private long _numberOfSegments;
        private long _numberOfAckedSegments;
        private bool _isFinished;
        private bool _isCancelled;
        private DateTime _startTime;
        private DateTime _finishTime;

        public Upload(string id, string fileName, long fileSize, string fileHash, CryptoPeer destination, long numberOfSegments)
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
        }

        public event EventHandler<UploadEventArgs>? UploadFinished;

        public string ID
        {
            get => _id;
            private set => SetProperty(ref _id, value);
        }

        public string FileName
        {
            get => _fileName;
            private set => SetProperty(ref _fileName, value);
        }

        public long FileSize
        {
            get => _fileSize;
            private set => SetProperty(ref _fileSize, value);
        }

        public string FileHash
        {
            get => _fileHash;
            private set => SetProperty(ref _fileHash, value);
        }
        
        public CryptoPeer Destination
        {
            get => _destination;
            private set => SetProperty(ref _destination, value);
        }

        public long NumberOfSegments
        {
            get => _numberOfSegments;
            private set => SetProperty(ref _numberOfSegments, value);
        }
        
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

        public decimal Progress => NumberOfAckedSegments / Convert.ToDecimal(NumberOfSegments);

        public void AddAck()
        {
            if (IsCancelled || IsFinished || (NumberOfAckedSegments == NumberOfSegments))
            {
                return;
            }

            NumberOfAckedSegments += 1;
            if (NumberOfAckedSegments == NumberOfSegments)
            {
                IsFinished = true;
                FinishTime = DateTime.Now;

                UploadFinished?.Invoke(this, new UploadEventArgs(ID));
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
    }
}
