namespace FileSharing.Networking
{
    public enum NetMessageTypes : byte
    {
        FilesListRequest = 10,
        FilesList,
        FileRequest,
        FileSegment,
        FileSegmentAck,
        CancelDownload,
    }
}
