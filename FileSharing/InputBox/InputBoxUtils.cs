using System.Net;

namespace InputBox
{
    public class InputBoxUtils
    {
        public bool AskServerAddress(out IPAddress? address)
        {
            var inputBox = new InputBoxWindow("Connection to File Server", "Enter IP address of File Server");
            if (inputBox.ShowDialog() == true)
            {
                if (IPAddress.TryParse(inputBox.Answer, out IPAddress? ip) &&
                    ip != null)
                {
                    address = ip;
                    return true;
                }
            }

            address = null;
            return false;
        }

        public bool AskPort(out int port)
        {
            var inputBox = new InputBoxWindow("Set server port number", "Enter port number of local File Server", 55000 + "");
            if (inputBox.ShowDialog() == true)
            {
                if (int.TryParse(inputBox.Answer, out int portNumber) &&
                    portNumber > 1024 &&
                    portNumber < 65536)
                {
                    port = portNumber;
                    return true;
                }
            }

            port = 0;
            return false;
        }

        public bool AskServerAddressAndPort(out IPEndPoint? serverAddress)
        {
            var inputBox = new InputBoxWindow("Connection to File Server",
                "Enter IP address of file server (example: 10.0.0.8).\nAlso you can specify port (example: 10.0.0.8:55000),\nwhere port takes values from 1025 to 65535.");
            if (inputBox.ShowDialog() == true)
            {
                if (IPAddress.TryParse(inputBox.Answer, out IPAddress? address) &&
                    address != null)
                {
                    serverAddress = new IPEndPoint(address, 55000);
                    return true;
                }

                if (IPEndPoint.TryParse(inputBox.Answer, out IPEndPoint? endPoint) &&
                    endPoint != null &&
                    endPoint.Port > 1024)
                {
                    serverAddress = endPoint;
                    return true;
                }
            }

            serverAddress = null;
            return false;
        }
    }
}
