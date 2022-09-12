namespace FileSharing
{
    public static class Constants
    {
        public static int FileSegmentSize { get; } = 1300;
        public static byte ChannelsCount { get; } = 64;
        public static int DisconnectionTimeout { get; } = 15000;
    }
}
