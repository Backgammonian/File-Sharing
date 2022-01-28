using LiteNetLib;

namespace FileSharing.Models
{
    public class FileInfo
    {
        public FileInfo()
        {
            Name = string.Empty;
            Size = 0;
            Hash = string.Empty;
            Server = null;
        }

        public FileInfo(string name, long size, string hash, NetPeer server)
        {
            Name = name;
            Size = size;
            Hash = hash;
            Server = server;
        }

        public FileInfo(SharedFile sharedFile)
        {
            Name = sharedFile.Name;
            Size = sharedFile.Size;
            Hash = sharedFile.Hash;
            Server = null;
        }

        public string Name { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public NetPeer? Server { get; set; }
    }
}
