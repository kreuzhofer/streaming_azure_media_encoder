using System;

namespace LargeFileUploader
{
    public class BlobMetadata
    {
        public BlobMetadata(int id, long length, int bytesPerChunk)
        {
            this.Id = id;
            this.BlockId = Convert.ToBase64String(System.BitConverter.GetBytes(id));
            this.Index = ((long)id) * ((long)bytesPerChunk);
            long remainingBytesInFile = length - this.Index;
            this.Length = (int)Math.Min(remainingBytesInFile, (long)bytesPerChunk);
        }

        public long Index { get; private set; }
        public int Id { get; private set; }
        public string BlockId { get; private set; }
        public long Length { get; private set; }
    }
}