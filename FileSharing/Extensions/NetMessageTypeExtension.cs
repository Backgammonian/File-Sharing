using System;
using FileSharing.Networking;

namespace Extensions
{
    public static class NetMessageTypeExtension
    {
        public static bool TryParseType(this byte typeByte, out NetMessageType type)
        {
            if (Enum.TryParse(typeByte + "", out NetMessageType messageType))
            {
                type = messageType;

                return true;
            }
            else
            {
                type = NetMessageType.None;

                return false;
            }
        }
    }
}
