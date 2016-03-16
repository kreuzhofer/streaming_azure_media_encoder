using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LargeFileUploader;

namespace AzureChunkingMediaFileUploader
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length<3)
            {
                Help();
                return -1;
            }

            StartUpload(args);

            Console.ReadLine();
            return 0;
        }

        private static async void StartUpload(string[] args)
        {
            var fileToUpload = args[0];
            var connectionString = args[1];
            var profileFileName = args[2];

            var jobId = Guid.NewGuid().ToString();

            ChunkingFileUploaderUtils.Log = Console.WriteLine;
            await ChunkingFileUploaderUtils.UploadAsync(jobId, fileToUpload, connectionString, profileFileName);
        }

        private static void Help()
        {
            Console.WriteLine("Azure Chunking Media File Uploader");
            Console.WriteLine("USAGE: AzureChunkingMediaFileUploader.exe <UploadFile> <Container> <ConnectionString>");
        }
    }
}
