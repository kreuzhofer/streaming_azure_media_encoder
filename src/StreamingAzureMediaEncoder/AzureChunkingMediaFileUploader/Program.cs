using System;
using System.Collections.Generic;
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
            if(args.Length<5)
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
            var containerName = args[1];
            var connectionString = args[2];
            var encoderParameters = args[3];
            var targetFilename = args[4];

            LargeFileUploaderUtils.Log = Console.WriteLine;
            await LargeFileUploaderUtils.UploadAsync(fileToUpload, connectionString, containerName, encoderParameters, targetFilename);
        }

        private static void Help()
        {
            Console.WriteLine("Azure Chunking Media File Uploader");
            Console.WriteLine("USAGE: AzureChunkingMediaFileUploader.exe <UploadFile> <Container> <ConnectionString>");
        }
    }
}
