﻿using System;
using System.Net;
using System.Net.Sockets;

namespace Helpers
{
    public static class LocalAddressResolver
    {
        public static IPAddress GetLocalAddress()
        {
            try
            {
                using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);

                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address;
                }
            }
            catch (Exception)
            {
            }

            return IPAddress.Any;
        }
    }
}