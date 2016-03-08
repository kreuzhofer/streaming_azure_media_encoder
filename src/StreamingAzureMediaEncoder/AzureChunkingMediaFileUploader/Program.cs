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
            if(args.Length<3)
            {
                Help();
                return -1;
            }

            var fileToUpload = args[0];
            var containerName = args[1];
            var connectionString = args[2];

            LargeFileUploaderUtils.Log = Console.WriteLine;
            LargeFileUploaderUtils.UploadAsync(fileToUpload, connectionString, containerName);

            Console.ReadLine();
            return 0;
        }

        private static void Help()
        {
            Console.WriteLine("Azure Chunking Media File Uploader");
            Console.WriteLine("USAGE: AzureChunkingMediaFileUploader.exe <UploadFile> <Container> <ConnectionString>");
        }
    }
}
