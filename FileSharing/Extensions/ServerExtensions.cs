using System.Collections.Generic;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using FileSharing.Models;
using FileSharing.Networking;

namespace Extensions
{
    public static class ServerExtensions
    {
        public static void SendFilesListToAllClients(this Server server, List<SharedFileInfo> filesList)
        {
            var filesListJson = JsonConvert.SerializeObject(filesList);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(filesListJson);

            server.SendToAll(message);
        }

        public static void SendFilesList(this EncryptedPeer client, List<SharedFileInfo> filesList)
        {
            var filesListJson = JsonConvert.SerializeObject(filesList);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesList);
            message.Put(filesListJson);

            client.SendEncrypted(message, 0);
        }
    }
}
