using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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

        public static void SendFileSegmentAck(this EncryptedPeer server, string downloadID, long numOfSegment)
        {
            Debug.WriteLine($"(SendFileSegmentAck) {downloadID}");

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FileSegmentAck);
            message.Put(downloadID);
            message.Put(numOfSegment);

            server.SendEncrypted(message, 0);
        }

        public static void RequestFileSegment(this EncryptedPeer server, string downloadID, string fileHash, long numOfSegment)
        {
            Debug.WriteLine("(RequestFileSegment) " + fileHash + ", segment №" + numOfSegment);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.ResendFileSegment);
            message.Put(downloadID);
            message.Put(fileHash);
            message.Put(numOfSegment);

            server.SendEncrypted(message, 0);
        }
    }
}
