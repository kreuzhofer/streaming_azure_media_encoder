using System;

namespace LargeFileUploader
{
    public class EncodingTaskMetaData
    {
        public string JobId { get; set; }
        public string TaskId { get; set; }
        public string BlobName { get; set; }
        public long Length { get; set; }
        public string EncoderParameters { get; set; }
        public string TargetFilename { get; set; }
        public string SourceContainerSas { get; set; }
        public Uri SourceContainerUri { get; set; }
        public string JobQueueSas { get; set; }
        public Uri JobQueueUri { get; set; }
        public string TargetContainerSas { get; set; }
        public Uri TargetContainerUri { get; set; }
        public string TableSas { get; set; }
        public Uri TableUri { get; set; }
        public int EncoderTimeout { get; set; }

        public int RenditionIndex { get; set; }
        public Uri CallbackUri { get; set; }
    }
}