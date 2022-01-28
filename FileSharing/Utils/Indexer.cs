namespace FileSharing.Utils
{
    public class Indexer
    {
        private long _currentIndex;

        public Indexer()
        {
            _currentIndex = -1;
        }

        public long GetIndex()
        {
            _currentIndex += 1;
            _currentIndex = _currentIndex == long.MaxValue ? 0 : _currentIndex;
            return _currentIndex;
        }
    }
}
