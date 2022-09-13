using System.Net;

namespace InputBox
{
    public static class InputBoxExtensions
    {
        public static bool AskServerAddress(this InputBoxWindow window, out IPAddress? address)
        {
            address = null;

            window.TitleText = "Connection to File Server";
            window.QuestionText = "Enter IP address of File Server";
            window.AnswerText = string.Empty;

            if (window.ShowDialog() == true)
            {
                if (IPAddress.TryParse(window.AnswerText, out IPAddress? ip) &&
                    ip != null)
                {
                    address = ip;

                    return true;
                }
            }

            return false;
        }

        public static bool AskPort(this InputBoxWindow window, int defaultPort, out int port)
        {
            port = 0;

            window.TitleText = "Set server port number";
            window.QuestionText = "Enter port number of local File Server";
            window.AnswerText = defaultPort + string.Empty;

            if (window.ShowDialog() == true)
            {
                if (int.TryParse(window.AnswerText, out int portNumber) &&
                    portNumber > 1024 &&
                    portNumber < 65536)
                {
                    port = portNumber;

                    return true;
                }
            }

            return false;
        }

        public static bool AskServerAddressAndPort(this InputBoxWindow window, IPEndPoint defaultEndPoint, out IPEndPoint? serverAddress)
        {
            serverAddress = null;

            window.TitleText = "Connection to File Server";
            window.QuestionText = $"Enter IP address of file server (example: {defaultEndPoint.Address}).\n" +
                $"Also you can specify port (example: {defaultEndPoint}),\n" +
                "where port takes values from 1025 to 65535.";
            window.AnswerText = defaultEndPoint + string.Empty;

            if (window.ShowDialog() == true)
            {
                if (IPAddress.TryParse(window.AnswerText, out IPAddress? address) &&
                    address != null)
                {
                    serverAddress = new IPEndPoint(address, 55000);

                    return true;
                }

                if (IPEndPoint.TryParse(window.AnswerText, out IPEndPoint? endPoint) &&
                    endPoint != null &&
                    endPoint.Port > 1024)
                {
                    serverAddress = endPoint;

                    return true;
                }
            }

            return false;
        }
    }
}
