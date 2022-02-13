using System;

namespace FileSharing
{
    public static class Constants
    {
        public static readonly int FileSegmentSize = Convert.ToInt32(Math.Pow(2, 18));
        public const byte ChannelsCount = 8;
        public const int DisconnectionTimeout = 30000;
    }
}
