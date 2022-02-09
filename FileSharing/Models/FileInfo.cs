namespace FileSharing.Models
{
    public class FileInfo
    {
        public FileInfo() //empty constructor for JSON deserializing, used in client
        {
            Name = "";
            Size = 0;
            NumberOfSegments = 0;
            Hash = "";
            Server = null;
        }

        public FileInfo(SharedFile sharedFile) //used in server
        {
            Name = sharedFile.Name;
            Size = sharedFile.Size;
            NumberOfSegments = sharedFile.NumberOfSegments;
            Hash = sharedFile.Hash;
            Server = null;
        }

        public string Name { get; set; }
        public long Size { get; set; }
        public long NumberOfSegments { get; set; }
        public string Hash { get; set; }
        public EncryptedPeer? Server { get; set; }
    }
}