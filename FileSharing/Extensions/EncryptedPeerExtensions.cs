using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using LiteNetLib.Utils;
using FileSharing;
using FileSharing.Networking;
using FileSharing.Models;

namespace Extensions
{
    public static class EncryptedPeerExtensions
    {
        public static void SendUploadDenial(this EncryptedPeer destination, string uploadID)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.CancelDownload);
            message.Put(uploadID);

            destination.SendEncrypted(message, 0);
        }

        public static void  SendFilesListRequest(this EncryptedPeer server)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FilesListRequest);

            server.SendEncrypted(message, 0);
        }

        public static void SendFileSegmentAck(this EncryptedPeer server, string downloadID, long numOfSegment, byte channel)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FileSegmentAck);
            message.Put(downloadID);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message, channel);
        }

        public static void RequestFileSegment(this EncryptedPeer server, string downloadID, string fileHash, long numOfSegment, byte channel)
        {
            Debug.WriteLine("(RequestFileSegment) " + fileHash + ", segment no. " + numOfSegment);

            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.ResendFileSegment);
            message.Put(downloadID);
            message.Put(fileHash);
            message.Put(numOfSegment);
            message.Put(channel);

            server.SendEncrypted(message.Data, channel);
        }

        public static void SendFileRequest(this EncryptedPeer server, Download download)
        {
            var message = new NetDataWriter();
            message.Put((byte)NetMessageType.FileRequest);
            message.Put(download.Hash);
            message.Put(download.ID);

            server.SendEncrypted(message, 0);
        }

        public static async Task RequestMissingFileSegments(this EncryptedPeer server, string downloadID, string fileHash, List<long> numbersOfMissingSegments)
        {
            for (int i = 0; i < numbersOfMissingSegments.Count; i++)
            {
                server.RequestFileSegment(downloadID,
                    fileHash,
                    numbersOfMissingSegments[i],
                    Convert.ToByte(i % Constants.ChannelsCount));

                await Task.Delay(10);
            }
        }
    }
}
