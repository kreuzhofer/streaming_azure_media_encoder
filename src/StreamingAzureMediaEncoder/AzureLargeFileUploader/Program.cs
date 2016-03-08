using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LargeFileUploader;

namespace AzureLargeFileUploader
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3)
            {
                Help();
                return -1;
            }

            var fileToUpload = args[0];
            var containerName = args[1];
            var connectionString = args[2];

            LargeFileUploaderUtils.UploadAsync(fileToUpload, connectionString, containerName, (sender, i) =>
            {
                Console.WriteLine(i);
            });

            Console.ReadLine();
            return 0;
        }

        private static void Help()
        {
            Console.WriteLine("Azure Large File Uploader");
            Console.WriteLine("USAGE: AzureLargeFileUploader.exe <UploadFile> <Container> <ConnectionString>");
        }
    }
}
