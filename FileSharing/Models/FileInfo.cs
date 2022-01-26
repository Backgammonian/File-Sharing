using System.Net;
using LiteNetLib;

namespace FileSharing.Models
{
    public class FileInfo
    {
        public FileInfo()
        {
            Name = "";
            Size = 0;
            Hash = "";
        }

        public FileInfo(string name, long size, string hash, NetPeer server)
        {
            Name = name;
            Size = size;
            Hash = hash;
            Server = server;
        }

        public string Name { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public NetPeer? Server { get; set; }
    }
}
