using LargeFileUploader;

namespace AzureChunkingMediaFileUploader
{
    public static class Constants
    {
        public const int PADDING = 3;
        public const char SEPERATOR = '_';
        public static int NumBytesPerChunk = 4 * MB; // A block may be up to 4 MB in size. 
        public const int MAXIMUM_UPLOAD_SIZE = 4 * MB;

        public const int kB = 1024;
        public const int MB = kB * 1024;
        public const long GB = MB * 1024;
    }
}