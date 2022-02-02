namespace FileSharing.Networking
{
    public enum NetMessageType : byte
    {
        None = 0,
        FilesListRequest = 10,
        FilesList,
        FileRequest,
        FileSegment,
        FileSegmentAck,
        CancelDownload,
    }
}
