using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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
                catch (Exception)
                {
                    Console.WriteLine("Waiting for task...");
                    Thread.Sleep(1000);
                }
            }
        }

        private static async void StartDownloadAndEncode(UploadMetaData uploadMetaData, CloudStorageAccount storageAccount)
        {
            await StartDownloadAndEncodeAsync(uploadMetaData, storageAccount);
        }

        private static async Task StartDownloadAndEncodeAsync(UploadMetaData uploadMetaData, CloudStorageAccount storageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(uploadMetaData.ContainerName);
            container.CreateIfNotExists();

            // check if file exists and delete
            if (File.Exists(uploadMetaData.TargetFilename))
            {
                File.Delete(uploadMetaData.TargetFilename);
            }

            var inputPipeReady = new AutoResetEvent(false);

            // start named pipe server
            var inputPipeTask = Task.Factory.StartNew(async () =>
            {
                var server = new NamedPipeServerStream(uploadMetaData.BlobName);
                inputPipeReady.Set();
                server.WaitForConnection();

                var allBlobsInFile = Enumerable
                    .Range(0, 1 + ((int) (uploadMetaData.Length/Constants.NumBytesPerChunk)))
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
                        await Task.Delay(1000);
                    }

                    var fileName = uploadMetaData.BlobName + Constants.SEPERATOR +
                                   metaData.Id.ToString().PadLeft(Constants.PADDING, '0');
                    var blobRef = container.GetBlobReference(fileName);

                    var buffer = new byte[Constants.NumBytesPerChunk];
                    blobRef.DownloadToByteArray(buffer, 0);

                    await server.WriteAsync(buffer, 0, (int)metaData.Length);
                    await server.FlushAsync();
                }
                server.Disconnect();
            });

            inputPipeReady.WaitOne();

            var ffmpeg = new FileInfo("ffmpeg.exe");
            string ffmpegArgs = @"-i \\.\pipe\{2} {0} {1}";
            var ffmpegArgsFormatted = String.Format(ffmpegArgs, uploadMetaData.EncoderParameters, uploadMetaData.TargetFilename, uploadMetaData.BlobName);
            var startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                FileName = ffmpeg.ToString(),
                Arguments = ffmpegArgsFormatted,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var process = new Process {StartInfo = startInfo, EnableRaisingEvents = true};

            process.OutputDataReceived += (sender, args) => { Console.WriteLine(args.Data); };
            process.ErrorDataReceived += (sender, args) => { Console.WriteLine(args.Data); };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

            process.WaitForExit();
            Task.WaitAll(new[] {inputPipeTask}); // wait for all pipe tasks to complete
            Console.WriteLine("Done");
        }
    }
}
