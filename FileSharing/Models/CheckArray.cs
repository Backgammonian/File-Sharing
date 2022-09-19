using System;
using System.Diagnostics;

namespace FileSharing.Models
{
    public sealed class CheckArray
    {
        private readonly bool[] _array;
        private long _filledPositions;
        private decimal _progress;
        private bool _isFilled;

        public CheckArray(long length)
        {
            _array = new bool[length];
            for (long i = 0; i < Length; i++)
            {
                _array[i] = false;
            }

            _isFilled = false;
            _filledPositions = 0;
            _progress = 0;
        }

        public event EventHandler<EventArgs>? Filled;

        public long Length => _array.LongLength;
        public decimal Progress => _progress;
        public bool IsFilled => _isFilled;

        public bool this[long position]
        {
            get => _array[position];
            private set => _array[position] = value;
        }

        private bool CheckIsFilled()
        {
            for (long i = 0; i < Length; i++)
            {
                if (this[i] == false)
                {
                    return false;
                }
            }

            return true;
        }

        public void Add(long position)
        {
            Debug.WriteLine($"(CheckArray_Add) {position}");

            if (_isFilled)
            {
                return;
            }

            if (this[position])
            {
                return;
            }

            this[position] = true;
            _filledPositions += 1;
            _progress = _filledPositions / Convert.ToDecimal(Length);

            if (CheckIsFilled())
            {
                Filled?.Invoke(this, EventArgs.Empty);

                _isFilled = true;
            }
        }

        public long GetFreePosition(byte channelNumber)
        {
            if (_isFilled)
            {
                Debug.WriteLine($"(GetFreePosition_Result) -1");

                return -1;
            }

            //NumberOfSegments = Size / Constants.FileSegmentSize + (Size % Constants.FileSegmentSize != 0 ? 1 : 0);
            for (long i = channelNumber; i < Length; i += channelNumber)
            {
                if (this[i] == false)
                {
                    Debug.WriteLine($"(GetFreePosition_Result) {i}");

                    return i;
                }
            }

            for (long i = 0; i < Length; i++)
            {
                if (this[i] == false)
                {
                    Debug.WriteLine($"(GetFreePosition_Result) {i}");

                    return i;
                }
            }

            Debug.WriteLine($"(GetFreePosition_Result) -1");

            return -1;
        }
    }
}
