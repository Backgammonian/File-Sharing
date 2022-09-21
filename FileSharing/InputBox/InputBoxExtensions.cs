using System.Net;

namespace InputBox
{
    public static class InputBoxExtensions
    {
        public static IPAddress? AskServerAddress(this InputBoxWindow window)
        {
            window.TitleText = "Connection to File Server";
            window.QuestionText = "Enter IP address of File Server";
            window.AnswerText = string.Empty;

            if (window.ShowDialog() == true &&
                IPAddress.TryParse(window.AnswerText, out IPAddress? ip) &&
                ip != null)
            {
                return ip;
            }
            
            return null;
        }

        public static int AskPort(this InputBoxWindow window, int defaultPort)
        {
            window.TitleText = "Set server port number";
            window.QuestionText = "Enter port number of local File Server";
            window.AnswerText = defaultPort + string.Empty;

            if (window.ShowDialog() == true &&
                int.TryParse(window.AnswerText, out int portNumber) &&
                portNumber > 1024 &&
                portNumber < 65536)
            {
                return portNumber;
            }

            return -1;
        }

        public static IPEndPoint? AskServerAddressAndPort(this InputBoxWindow window, IPEndPoint defaultEndPoint)
        {
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
                    return new IPEndPoint(address, 55000);
                }

                if (IPEndPoint.TryParse(window.AnswerText, out IPEndPoint? endPoint) &&
                    endPoint != null &&
                    endPoint.Port > 1024 &&
                    endPoint.Port < 65536)
                {
                    return endPoint;
                }
            }

            return null;
        }
    }
}
