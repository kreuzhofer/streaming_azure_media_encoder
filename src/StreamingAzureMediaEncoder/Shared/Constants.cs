using LargeFileUploader;

namespace AzureChunkingMediaFileUploader
{
    public static class Constants
    {
        public const int PADDING = 3;
        public const char SEPERATOR = '_';
        public static int NumBytesPerChunk = 4 * MB; // A block may be up to 4 MB in size. 
        public static int ENCODER_TIMEOUT = 60;
        public static string TENANT = "tenant1";
        public const int MAXIMUM_UPLOAD_SIZE = 4 * MB;

        public const int kB = 1024;
        public const int MB = kB * 1024;
        public const long GB = MB * 1024;
        public const string TaskQueueName = "uploadnotifications";
        public const string TaskTableName = "encodingTasks";
        public const string JobTableName = "encodingJobs";

        public const string STATUS_CREATED = "CREATED";
        public const string STATUS_RUNNING = "RUNNING";
        public const string STATUS_DONE = "DONE";
        public const string STATUS_ABORTED = "ABORTED";
        public const string STATUS_UPLOADING = "UPLOADING";
        public static int PeekMessageCount = 25;
    }
}