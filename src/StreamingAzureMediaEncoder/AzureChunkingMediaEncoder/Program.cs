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
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace AzureChunkingMediaEncoder
{
    class Program
    {
        static void Main(string[] args)
        {
            var queueName = args[0];
            var queueSas = args[1];
            Console.WriteLine(queueName);
            Console.WriteLine(queueSas);

            var queue = new CloudQueue(new Uri(queueName), new StorageCredentials(queueSas));

            while (true)
            {
                try
                {
                    var message = queue.GetMessage();
                    Console.WriteLine(message.AsString);
                    queue.DeleteMessage(message);

                    StartDownloadAndEncode(JsonConvert.DeserializeObject<EncodingTaskMetaData>(message.AsString));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Waiting for task... {0}", ex);
                    Thread.Sleep(1000);
                }
            }
        }

        private static async void StartDownloadAndEncode(EncodingTaskMetaData encodingTaskMetaData)
        {
            await StartDownloadAndEncodeAsync(encodingTaskMetaData);
        }

        private static async Task StartDownloadAndEncodeAsync(EncodingTaskMetaData encodingTaskMetaData)
        {
            var sasContainerRef = new CloudBlobContainer(encodingTaskMetaData.SourceContainerUri, new StorageCredentials(encodingTaskMetaData.SourceContainerSas));

            // check if file exists and delete
            if (File.Exists(encodingTaskMetaData.TargetFilename))
            {
                File.Delete(encodingTaskMetaData.TargetFilename);
            }

            var inputPipeReady = new AutoResetEvent(false);
            var inputPipeName = Guid.NewGuid() + encodingTaskMetaData.BlobName;
            var cts = new CancellationTokenSource();

            // start named pipe server
            var inputPipeTask = Task.Factory.StartNew(async () =>
            {
                try
                {
                    var server = new NamedPipeServerStream(inputPipeName);
                    inputPipeReady.Set();
                    server.WaitForConnection();

                    var allBlobsInFile = Enumerable
                        .Range(0, 1 + ((int) (encodingTaskMetaData.Length/Constants.NumBytesPerChunk)))
                        .Select(_ => new BlobMetadata(_, encodingTaskMetaData.Length, Constants.NumBytesPerChunk))
                        .Where(block => block.Length > 0)
                        .ToList();

                    var existingBlobs =
                        sasContainerRef.ListBlobs()
                            .Select(
                                blob =>
                                    new BlobMetadata(
                                        int.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()),
                                        encodingTaskMetaData.Length, Constants.NumBytesPerChunk))
                            .ToList();

                    for (int i = 0; i < allBlobsInFile.Count; i++)
                    {
                        var metaData = allBlobsInFile[i];

                        var waitTimer = Stopwatch.StartNew();
                        while (existingBlobs.All(b => b.Id != i)) // wait for blob
                        {
                            existingBlobs =
                                sasContainerRef.ListBlobs()
                                    .Select(
                                        blob =>
                                            new BlobMetadata(
                                                int.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()),
                                                encodingTaskMetaData.Length, Constants.NumBytesPerChunk))
                                    .ToList();
                            await Task.Delay(1000);

                            // break if the next block does not arrive in certain time interval
                            if (waitTimer.Elapsed >= TimeSpan.FromSeconds(30) || cts.IsCancellationRequested)
                            {
                                cts.Cancel();
                                throw new Exception(String.Format("Aborting Task {0} of Job {1}",
                                    encodingTaskMetaData.TaskId, encodingTaskMetaData.JobId));
                            }
                        }

                        var fileName = encodingTaskMetaData.BlobName + Constants.SEPERATOR +
                                       metaData.Id.ToString().PadLeft(Constants.PADDING, '0');
                        var blobRef = sasContainerRef.GetBlobReference(fileName);

                        var buffer = new byte[Constants.NumBytesPerChunk];
                        blobRef.DownloadToByteArray(buffer, 0);

                        await server.WriteAsync(buffer, 0, (int) metaData.Length);
                        await server.FlushAsync();
                    }
                    server.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            });

            var ffmpegTask = Task.Factory.StartNew(() =>
            {
                inputPipeReady.WaitOne();

                var ffmpeg = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"));
                string ffmpegArgs = @"-i \\.\pipe\{2} {0} {1}";
                var ffmpegArgsFormatted = String.Format(ffmpegArgs, encodingTaskMetaData.EncoderParameters, encodingTaskMetaData.TargetFilename, inputPipeName);
                var startInfo = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    FileName = ffmpeg.ToString(),
                    Arguments = ffmpegArgsFormatted,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

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

                while (!cts.IsCancellationRequested)
                {
                    process.WaitForExit(1000);
                    if (process.HasExited)
                    {
                        break;
                    }
                    if (cts.IsCancellationRequested)
                    {
                        process.Kill();
                        process.Close();
                        throw new Exception("ffmpeg process killed because of cancellation request");
                    }
                }
                
            });

            try
            {
                Task.WaitAll(new[] { inputPipeTask, ffmpegTask }); // wait for all pipe tasks to complete
            }
            catch (Exception)
            {
                Console.WriteLine("Aborted");
                return;
            }
            Console.WriteLine("Encoding Done");

            // upload file to target folder
            var sasTargetFolderRef = new CloudBlobContainer(encodingTaskMetaData.TargetContainerUri, new StorageCredentials(encodingTaskMetaData.TargetContainerSas));
            await LargeFileUploaderUtils.UploadAsync(new FileInfo(encodingTaskMetaData.TargetFilename), sasTargetFolderRef, (sender, i) => { });
            File.Delete(encodingTaskMetaData.TargetFilename); // delete local file after upload
            Console.WriteLine("Upload done");
        }
    }
}
