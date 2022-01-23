using System.Net;

namespace FileSharing.Models
{
    public class FileInfo
    {
        public FileInfo()
        {
            Name = "";
            Size = 0;
            Hash = "";
            ServerID = -1;
            ServerAddress = new IPEndPoint(0, 0);
        }

        public FileInfo(string name, long size, string hash, int serverID, IPEndPoint serverAddress)
        {
            Name = name;
            Size = size;
            Hash = hash;
            ServerID = serverID;
            ServerAddress = serverAddress;
        }

        public string Name { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
        public int ServerID { get; set; }
        public IPEndPoint ServerAddress { get; set; }
    }
}
