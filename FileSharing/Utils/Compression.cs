using System;
using System.IO;
using System.IO.Compression;

namespace FileSharing.Utils
{
    public static class Compression
    {
        public static byte[] CompressByteArray(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                using var compressedStream = new MemoryStream();
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress);
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();

                return compressedStream.ToArray();
            }

            return Array.Empty<byte>();
        }

        public static byte[] DecompressByteArray(byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                using var compressedStream = new MemoryStream(data);
                using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var resultStream = new MemoryStream();
                zipStream.CopyTo(resultStream);

                return resultStream.ToArray();
            }

            return Array.Empty<byte>();
        }
    }
}
