using System;

namespace FileSharing
{
    public static class Constants
    {
        public static int FileSegmentSize { get; } = Convert.ToInt32(Math.Pow(2, 13));
        public static byte ChannelsCount { get; } = 32;
        public static int DisconnectionTimeout { get; } = 30000;
    }
}
