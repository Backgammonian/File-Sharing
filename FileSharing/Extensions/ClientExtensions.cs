using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using FileSharing;
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

        public static void SendFileSegmentAck(this EncryptedPeer server, string downloadID, long numOfSegment, byte channel)
        {
            Debug.WriteLine($"(SendFileSegmentAck) {downloadID}");

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FileSegmentAck);
            message.Put(downloadID);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message, channel);
        }

        public static void RequestFileSegment(this EncryptedPeer server, string downloadID, string fileHash, long numOfSegment, byte channel)
        {
            Debug.WriteLine("(RequestFileSegment) " + fileHash + ", segment №" + numOfSegment);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.ResendFileSegment);
            message.Put(downloadID);
            message.Put(fileHash);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message.Data, channel);
        }

        public static void RequestMissingFileSegments(this EncryptedPeer server, string downloadID, string fileHash, List<long> numbersOfMissingSegments)
        {
            var requestMissingFileSegmentsTask = new Task(async () =>
            {
                for (int i = 0; i < numbersOfMissingSegments.Count; i++)
                {
                    server.RequestFileSegment(downloadID,
                        fileHash,
                        numbersOfMissingSegments[i],
                        Convert.ToByte(i % Constants.ChannelsCount));

                    await Task.Delay(10);
                }
            });

            requestMissingFileSegmentsTask.Start();
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
