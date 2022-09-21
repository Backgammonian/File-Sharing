using System.Diagnostics;
using LiteNetLib.Utils;
using FileSharing.Networking;
using FileSharing.Models;

namespace Extensions
{
    public static class ClientExtensions
    {
        public static void SendFilesListRequest(this EncryptedPeer server)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesListRequest);

            server.SendEncrypted(message, 0);
        }

        public static void SendFileRequest(this EncryptedPeer server, Download download)
        {
            Debug.WriteLine($"(SendFileRequest) {download.FilePath}");

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FileRequest);
            message.Put(download.Hash);
            message.Put(download.ID);

            server.SendEncrypted(message, 0);
        }
    }
}
