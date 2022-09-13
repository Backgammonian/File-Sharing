namespace FileSharing.Models
{
    public sealed class Indexer
    {
        public Indexer()
        {
            CurrentIndex = 0;
        }

        public long CurrentIndex { get; private set; }

        public long GetNewIndex()
        {
            CurrentIndex += 1;
            CurrentIndex = CurrentIndex == long.MaxValue ? 0 : CurrentIndex;
            return CurrentIndex;
        }
    }
}
