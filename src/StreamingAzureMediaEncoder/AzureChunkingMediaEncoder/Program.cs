using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AzureChunkingMediaFileUploader;
using LargeFileUploader;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace AzureChunkingMediaEncoder
{
    class Program
    {
        static void Main(string[] args)
        {
            var queueName = args[0];
            var connectionString = args[1];

            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(queueName);
            queue.CreateIfNotExists();

            while (true)
            {
                try
                {
                    var message = queue.GetMessage();
                    Console.WriteLine(message.AsString);
                    queue.DeleteMessage(message);

                    StartDownloadAndEncode(JsonConvert.DeserializeObject<UploadMetaData>(message.AsString), storageAccount);
                }
                catch (Exception ex)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        private static void StartDownloadAndEncode(UploadMetaData uploadMetaData, CloudStorageAccount storageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(uploadMetaData.ContainerName);
            container.CreateIfNotExists();

            // check if file exists and delete
            if (File.Exists(uploadMetaData.BlobName))
            {
                File.Delete(uploadMetaData.BlobName);
            }

            var allBlobsInFile = Enumerable
                .Range(0, 1 + ((int) (uploadMetaData.Length / Constants.NumBytesPerChunk)))
                .Select(_ => new BlobMetadata(_, uploadMetaData.Length, Constants.NumBytesPerChunk))
                .Where(block => block.Length > 0)
                .ToList();
            var existingBlobs = container.ListBlobs().Select(blob => new BlobMetadata(int.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()), uploadMetaData.Length, Constants.NumBytesPerChunk)).ToList();

            for (int i = 0; i < allBlobsInFile.Count; i++)
            {
                var metaData = allBlobsInFile[i];

                while (existingBlobs.All(b => b.Id != i)) // wait for blob
                {
                    existingBlobs = container.ListBlobs().Select(blob => new BlobMetadata(int.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()), uploadMetaData.Length, Constants.NumBytesPerChunk)).ToList();
                    Task.Delay(1000);
                }

                var fileName = uploadMetaData.BlobName + Constants.SEPERATOR +
                               metaData.Id.ToString().PadLeft(Constants.PADDING, '0');
                var blobRef = container.GetBlobReference(fileName);

                blobRef.DownloadToFile(uploadMetaData.BlobName, FileMode.Append);
            }
            Console.WriteLine("Done");
            var hash = FileHasher.MD5Hash(uploadMetaData.BlobName);
            if (hash != uploadMetaData.Hash)
            {
                Console.WriteLine("Error. Filehash invalid.");
            }
            else
            {
                Console.WriteLine("Filehash ok");
            }
        }
    }
}
