namespace LargeFileUploader
{
    public class UploadMetaData
    {
        public string ContainerName { get; set; }
        public string BlobName { get; set; }
        public long Length { get; set; }
        public string Hash { get; set; }
        public string EncoderParameters { get; set; }
        public string TargetFilename { get; set; }
    }
}