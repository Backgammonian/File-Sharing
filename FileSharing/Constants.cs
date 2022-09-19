using System;

namespace FileSharing
{
    public static class Constants
    {
        public static int FileSegmentSize { get; } = Convert.ToInt32(Math.Pow(2, 16));
        public static byte MaxChannelsCount { get; } = 64;
        public static byte ChannelsCount { get; } = MaxChannelsCount;
        public static int DisconnectionTimeout { get; } = 15000;
        public static int SpeedTimerFrequency { get; } = 10;
    }
}
