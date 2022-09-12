using System;

namespace FileSharing
{
    public static class Constants
    {
        public static int FileSegmentSize { get; } = 1000;
        public static byte ChannelsCount { get; } = 32;
        public static int DisconnectionTimeout { get; } = 15000;
    }
}
