using System.Net;

namespace FileSharing.InputBox
{
    public class InputBoxUtils
    {
        public bool AskServerAddress(out IPAddress? address)
        {
            var inputBox = new InputBox("Connection to File Server", "Enter address of File Server");
            if (inputBox.ShowDialog() == true)
            {
                if (IPAddress.TryParse(inputBox.Answer, out IPAddress? ip) &&
                    ip != null)
                {
                    address = ip;
                    return true;
                }

                address = null;
                return false;
            }

            address = null;
            return false;
        }
    }
}
