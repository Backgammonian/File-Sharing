using System.Collections.Generic;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using FileSharing.Models;
using FileSharing.Networking;
using FileSharing.Networking.Utils;

namespace Extensions
{
    public static class ServerExtensions
    {
        public static void SendUploadDenial(this EncryptedPeer client, string uploadID)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(uploadID);

            client.SendEncrypted(message, 0);
        }

        public static void SendFilesListToAllClients(this Server server, List<SharedFileInfo> filesList)
        {
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            server.SendToAll(message);
        }

        public static void SendFilesList(this EncryptedPeer client, List<SharedFileInfo> filesList)
        {
            var filesListJson = JsonConvert.SerializeObject(filesList);
            var crc = CRC32.Compute(filesListJson);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(crc);
            message.Put(filesListJson);

            client.SendEncrypted(message, 0);
        }
    }
}
