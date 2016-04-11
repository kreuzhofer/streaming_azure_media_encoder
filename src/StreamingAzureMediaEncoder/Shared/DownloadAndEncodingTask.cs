using System;
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
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Shared;

namespace AzureChunkingMediaEncoder
{
    public class DownloadAndEncodingTask
    {
        public void Start(string storageAccount, string tempFolder, CancellationToken ct)
        {
            var storageAccountClient = CloudStorageAccount.Parse(storageAccount);
            var queueClient = storageAccountClient.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(Constants.TaskQueueName);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (queue.ApproximateMessageCount != null && queue.ApproximateMessageCount.Value > 0)
                    {
                        CloudQueueMessage messageWithLowestIndex = null;
                        do
                        {
                            // peek the queue to find the lowest available renditionindex
                            var allMessages = queue.PeekMessages(Constants.PeekMessageCount);
                            int lowestIndex = Int32.MaxValue;
                            foreach (var cloudQueueMessage in allMessages)
                            {
                                var messageObj =
                                    JsonConvert.DeserializeObject<EncodingTaskMetaData>(cloudQueueMessage.AsString);
                                var minIndex = messageObj.RenditionIndex;
                                if (minIndex < lowestIndex)
                                {
                                    messageWithLowestIndex = cloudQueueMessage;
                                }
                            }
                            // try to delete this message
                            try
                            {
                                queue.DeleteMessage(messageWithLowestIndex);
                            }
                            catch
                            {
                                messageWithLowestIndex = null;
                            }
                        } while (messageWithLowestIndex == null);

                        Console.WriteLine(messageWithLowestIndex.AsString);

                        StartDownloadAndEncode(JsonConvert.DeserializeObject<EncodingTaskMetaData>(messageWithLowestIndex.AsString),
                            tempFolder, ct);
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Waiting for task...");
                    Thread.Sleep(1000);
                }
            }
        }

        private void StartDownloadAndEncode(EncodingTaskMetaData encodingTaskMetaData, string tempFolder, CancellationToken mainCancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var sasContainerRef = new CloudBlobContainer(encodingTaskMetaData.SourceContainerUri, new StorageCredentials(encodingTaskMetaData.SourceContainerSas));

            // update table to indicate we are running
            var tableRef = new CloudTable(encodingTaskMetaData.TableUri, new StorageCredentials(encodingTaskMetaData.TableSas));
            var task = new EncodingTaskEntity(encodingTaskMetaData.JobId, encodingTaskMetaData.TaskId);
            task.Status = Constants.STATUS_RUNNING;
            task.TaskMetaData.SourceFileName = encodingTaskMetaData.BlobName;
            task.TaskMetaData.TargetFileName = encodingTaskMetaData.TargetFilename;
            task.TaskMetaData.StartTime = DateTime.UtcNow;
            task.TaskMetaData.EncoderParameters = encodingTaskMetaData.EncoderParameters;
            task.TaskMetaData.RenditionIndex = encodingTaskMetaData.RenditionIndex;
            var insertOperation = TableOperation.Insert(task);
            tableRef.Execute(insertOperation);

            // check if file exists and delete
            if (File.Exists(encodingTaskMetaData.TargetFilename))
            {
                File.Delete(encodingTaskMetaData.TargetFilename);
            }

            var inputPipeReady = new AutoResetEvent(false);
            var ffmpegCts = new CancellationTokenSource();
            var inputPipeName = Guid.NewGuid() + encodingTaskMetaData.BlobName;
            var logFfmpeg = new StringBuilder();
            var ffmpegError = false;
            string targetFilename = Path.Combine(tempFolder, encodingTaskMetaData.TargetFilename);

            // start named pipe server
            NamedPipeServerStream server = null;
            var inputPipeTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    server = new NamedPipeServerStream(inputPipeName);
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
                                        Int32.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()),
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
                                                Int32.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()),
                                                encodingTaskMetaData.Length, Constants.NumBytesPerChunk))
                                    .ToList();
                            Thread.Sleep(1000);

                            // break if the next block does not arrive in certain time interval
                            if (waitTimer.Elapsed >= TimeSpan.FromSeconds(encodingTaskMetaData.EncoderTimeout) || mainCancellationToken.IsCancellationRequested)
                            {
                                ffmpegCts.Cancel();
                                throw new Exception(String.Format("Aborting Task {0} of Job {1}",
                                    encodingTaskMetaData.TaskId, encodingTaskMetaData.JobId));
                            }
                        }

                        var fileName = encodingTaskMetaData.BlobName + Constants.SEPERATOR +
                                       metaData.Id.ToString().PadLeft(Constants.PADDING, '0');
                        var blobRef = sasContainerRef.GetBlobReference(fileName);

                        var buffer = new byte[Constants.NumBytesPerChunk];
                        blobRef.DownloadToByteArray(buffer, 0);

                        server.Write(buffer, 0, (int) metaData.Length);
                        server.Flush();

                        // update status
                        task.Status = Constants.STATUS_RUNNING;
                        task.Progress = (i + 1)*100 / allBlobsInFile.Count;
                        var updateOperation = TableOperation.Replace(task);
                        tableRef.Execute(updateOperation);

                    }
                    server.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw;
                }
            }, mainCancellationToken);

            var ffmpegTask = Task.Factory.StartNew(() =>
            {
                inputPipeReady.WaitOne();

                var ffmpeg = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"));
                string ffmpegArgs = @"-i \\.\pipe\{2} {0} {1}";
                var ffmpegArgsFormatted = String.Format(ffmpegArgs, encodingTaskMetaData.EncoderParameters, targetFilename, inputPipeName);
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

                process.OutputDataReceived += (sender, args) =>
                {
                    Console.WriteLine(args.Data);
                    logFfmpeg.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (sender, args) =>
                {
                    Console.WriteLine(args.Data);
                    logFfmpeg.AppendLine(args.Data);
                };

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

                while (!mainCancellationToken.IsCancellationRequested && !ffmpegCts.IsCancellationRequested)
                {
                    process.WaitForExit(1000);
                    if (process.HasExited)
                    {
                        if (process.ExitCode != 0)
                        {
                            Thread.Sleep(1000);
                            ffmpegError = true;
                        }
                        break;
                    }
                    if (mainCancellationToken.IsCancellationRequested || ffmpegCts.IsCancellationRequested)
                    {
                        process.Kill();
                        process.Close();
                        throw new Exception("ffmpeg process killed because of cancellation request");
                    }
                }
                
            }, mainCancellationToken);

            try
            {
                Task.WaitAny(new[] { inputPipeTask, ffmpegTask }); // wait for all pipe tasks to complete
            }
            catch (Exception)
            {
                // Status is ABORTED
                task.Status = Constants.STATUS_ABORTED;
                task.TaskMetaData.FfmpegLog = logFfmpeg.ToString();
                task.TaskMetaData.EndTime = DateTime.UtcNow;
                task.TaskMetaData.Duration = watch.Elapsed;
                var updateOperation2 = TableOperation.Replace(task);
                tableRef.Execute(updateOperation2);

                Console.WriteLine("Aborted");
                return;
            }
            if (ffmpegTask.Status == TaskStatus.Running)
            {
                Task.WaitAll(ffmpegTask);
            }
            if (ffmpegError)
            {
                // Status is ABORTED
                task.Status = Constants.STATUS_ABORTED;
                task.TaskMetaData.FfmpegLog = logFfmpeg.ToString();
                task.TaskMetaData.EndTime = DateTime.UtcNow;
                task.TaskMetaData.Duration = watch.Elapsed;
                var updateOperation2 = TableOperation.Replace(task);
                tableRef.Execute(updateOperation2);
                return;
            }
            Console.WriteLine("Encoding Done");

            // Update status to UPLOADING
            task.Status = Constants.STATUS_UPLOADING;
            var updateOperation4 = TableOperation.Replace(task);
            tableRef.Execute(updateOperation4);

            // upload file to target folder
            var sasTargetFolderRef = new CloudBlobContainer(encodingTaskMetaData.TargetContainerUri, new StorageCredentials(encodingTaskMetaData.TargetContainerSas));
            var uploadTask = LargeFileUploaderUtils.UploadAsync(new FileInfo(targetFilename), sasTargetFolderRef, (sender, i) => { });
            Task.WaitAll(uploadTask);
            File.Delete(targetFilename); // delete local file after upload
            Console.WriteLine("Upload done");

            // Update status to DONE
            task.Status = Constants.STATUS_DONE;
            task.Progress = 100;
            task.TaskMetaData.EndTime = DateTime.UtcNow;
            task.TaskMetaData.Duration = watch.Elapsed;
            var updateOperation3 = TableOperation.Replace(task);
            tableRef.Execute(updateOperation3);
        }
    }
}