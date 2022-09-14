namespace FileSharing
{
    public static class Constants
    {
        public static int FileSegmentSize { get; } = 32768;
        public static byte MaxChannelsCount { get; } = 64;
        public static byte ChannelsCount { get; } = MaxChannelsCount;
        public static int DisconnectionTimeout { get; } = 15000;
    }
}
