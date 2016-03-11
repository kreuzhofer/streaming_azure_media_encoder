using System.Linq.Expressions;
using System.Net;
using System.Security.Policy;
using AzureChunkingMediaFileUploader;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace LargeFileUploader
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using global::Microsoft.WindowsAzure.Storage;
    using global::Microsoft.WindowsAzure.Storage.Blob;

    public static class LargeFileUploaderUtils
    {
        public static Action<string> Log { get; set; }
        public static void UseConsoleForLogging() { Log = Console.Out.WriteLine; }
        const uint DEFAULT_PARALLELISM = 1;

        public static Task<string> UploadAsync(string inputFile, string storageConnectionString, string containerName, string encoderParameters, string targetFilename, uint uploadParallelism = DEFAULT_PARALLELISM)
        {
            var hash = FileHasher.MD5Hash(inputFile);
            return (new FileInfo(inputFile)).UploadAsync(CloudStorageAccount.Parse(storageConnectionString), containerName, hash, encoderParameters, targetFilename, uploadParallelism);
        }

        public static Task<string> UploadAsync(this FileInfo file, CloudStorageAccount storageAccount, string containerName, string hash, string encoderParameters, string targetFilename, uint uploadParallelism = DEFAULT_PARALLELISM)
        {
            return UploadAsync(
                fetchLocalData: (offset, length) => file.GetFileContentAsync(offset, (int) length),
                blobLength: file.Length,
                storageAccount: storageAccount,
                containerName: containerName,
                blobName: file.Name,
                hash: hash,
                encoderParameters: encoderParameters,
                targetFilename: targetFilename,
                uploadParallelism: uploadParallelism);
        }

        public static async Task<string> UploadAsync(Func<long, long, Task<byte[]>> fetchLocalData, long blobLength,
            CloudStorageAccount storageAccount, string containerName, string blobName, string hash, string encoderParameters,
            string targetFilename, uint uploadParallelism = DEFAULT_PARALLELISM) 
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            if (Constants.NumBytesPerChunk > Constants.MAXIMUM_UPLOAD_SIZE)
            {
                Constants.NumBytesPerChunk = Constants.MAXIMUM_UPLOAD_SIZE;
            }

            #region Which blocks exist in the file

            var allBlobsInFile = Enumerable
                 .Range(0, 1 + ((int)(blobLength /Constants.NumBytesPerChunk)))
                 .Select(_ => new BlobMetadata(_, blobLength, Constants.NumBytesPerChunk))
                 .Where(block => block.Length > 0)
                 .ToList();

            #endregion

            #region Which blocks are already uploaded

            var existingBlobs = container.ListBlobs().Select(blob=> new BlobMetadata(int.Parse(blob.Uri.Segments.Last().Split(Constants.SEPERATOR).Last()), blobLength, Constants.NumBytesPerChunk)).ToList();
            List<BlobMetadata> missingBlobs = null;
            try
            {
                missingBlobs = allBlobsInFile.Where(blobInFile => existingBlobs.All(existingBlob => existingBlob.BlockId != blobInFile.BlockId)).ToList();
            }
            catch (StorageException)
            {
                missingBlobs = allBlobsInFile;
            }

            #endregion

            Func<BlobMetadata, Statistics, Task> uploadBlockAsync = async (block, stats) =>
            {
                byte[] blockData = await fetchLocalData(block.Index, block.Length);
                string contentHash = md5()(blockData);

                DateTime start = DateTime.UtcNow;

                await ExecuteUntilSuccessAsync(async () =>
                {
                    var blockBlob =
                        container.GetBlockBlobReference(blobName + Constants.SEPERATOR + block.Id.ToString().PadLeft(Constants.PADDING, '0'));
                    await blockBlob.PutBlockAsync(
                        blockId: block.BlockId,
                        blockData: new MemoryStream(blockData, true),
                        contentMD5: contentHash,
                        accessCondition: AccessCondition.GenerateEmptyCondition(),
                        options: new BlobRequestOptions
                        {
                            StoreBlobContentMD5 = true,
                            UseTransactionalMD5 = true
                        },
                        operationContext: new OperationContext());
                    await blockBlob.PutBlockListAsync(new[] {block.BlockId});
                }, consoleExceptionHandler);

                stats.Add(block.Length, start);
            };

            var s = new Statistics(missingBlobs.Sum(b => b.Length));

            try
            {
                // queue a new message to notifiy the downloaders about the new upload
                var queueClient = storageAccount.CreateCloudQueueClient();
                var queue = queueClient.GetQueueReference("uploadnotifications");
                queue.CreateIfNotExists();
                var metaData = new UploadMetaData()
                {
                    BlobName = blobName,
                    Length = blobLength,
                    ContainerName = containerName,
                    Hash = hash,
                    EncoderParameters = encoderParameters,
                    TargetFilename = targetFilename,
                };
                queue.AddMessage(new CloudQueueMessage(JsonConvert.SerializeObject(metaData)));
            }
            catch (Exception ex)
            {
                log("Error {0}", ex.Message);
                throw;
            }

            await LargeFileUploaderUtils.ForEachAsync(
                source: missingBlobs,
                parallelUploads: 4,
                body: blockMetadata => uploadBlockAsync(blockMetadata, s));

            log("PutBlockList succeeded, finished upload to {0}", containerName);

            return blobName;
        }

        internal static void log(string format, params object[] args)
        {
            if (Log != null) { Log(string.Format(format, args)); }
        }

        public static async Task<byte[]> GetFileContentAsync(this FileInfo file, long offset, int length)
        {
            using (var stream = file.OpenRead())
            {
                stream.Seek(offset, SeekOrigin.Begin);

                byte[] contents = new byte[length];
                var len = await stream.ReadAsync(contents, 0, contents.Length);
                if (len == length)
                {
                    return contents;
                }

                byte[] rest = new byte[len];
                Array.Copy(contents, rest, len);
                return rest;
            }
        }

        internal static void consoleExceptionHandler(Exception ex)
        {
            log("Problem occured, trying again. Details of the problem: ");
            for (var e = ex; e != null; e = e.InnerException)
            {
                log(e.Message);
            }
            log("---------------------------------------------------------------------");
            log(ex.StackTrace);
            log("---------------------------------------------------------------------");
        }

        public static async Task ExecuteUntilSuccessAsync(Func<Task> action, Action<Exception> exceptionHandler)
        {
            bool success = false;
            while (!success)
            {
                
                try
                {
                    await action();
                    success = true;
                }
                catch (Exception ex)
                {
                    if (exceptionHandler != null) { exceptionHandler(ex); }
                }
            }
        }

        internal static Task ForEachAsync<T>(this IEnumerable<T> source, int parallelUploads, Func<T, Task> body)
        {
            return Task.WhenAll(
                Partitioner
                .Create(source)
                .GetPartitions(parallelUploads)
                .Select(partition => Task.Run(async () =>
                {
                    using (partition)
                    {
                        while (partition.MoveNext())
                        {
                            await body(partition.Current);
                        }
                    }
                })));
        }

        internal static Func<byte[], string> md5()
        {
            var hashFunction = MD5.Create();

            return (content) => Convert.ToBase64String(hashFunction.ComputeHash(content));
        }




        public class Statistics
        {
            public Statistics(long totalBytes) { this.TotalBytes = totalBytes; }

            internal readonly DateTime InitialStartTime = DateTime.UtcNow;
            
            internal readonly object _lock = new object();
            internal long TotalBytes { get; private set; }
            internal long Done { get; private set; }

            internal void Add(long moreBytes, DateTime start)
            {
                long done;
                lock (_lock)
                {
                    this.Done += moreBytes;
                    done = this.Done;
                }

                var kbPerSec = (((double)moreBytes) / (DateTime.UtcNow.Subtract(start).TotalSeconds *Constants.kB));
                var MBPerMin = (((double)moreBytes) / (DateTime.UtcNow.Subtract(start).TotalMinutes *Constants.MB));

                log(
                    "Uploaded {0} ({1}) with {2} kB/sec ({3} MB/min), {4}",
                    absoluteProgress(done, this.TotalBytes),
                    relativeProgress(done, this.TotalBytes),
                    kbPerSec.ToString("F0"),
                    MBPerMin.ToString("F1"),
                    estimatedArrivalTime()
                    );
            }

            internal string estimatedArrivalTime()
            {
                var now = DateTime.UtcNow;

                double elapsedSeconds = now.Subtract(InitialStartTime).TotalSeconds;
                double progress = ((double)this.Done) / ((double)this.TotalBytes);

                if (this.Done == 0) return "unknown time";

                double remainingSeconds = elapsedSeconds * (1 - progress) / progress;

                TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);

                return string.Format("{0} remaining, (expect to finish by {1} local time)",
                    remaining.ToString("g"),
                    now.ToLocalTime().Add(remaining));
            }

            private static string absoluteProgress(long current, long total)
            {
                if (total < Constants.kB)
                {
                    // Bytes is reasonable
                    return string.Format("{0} of {1} bytes", current, total);
                }
                else if (total < 10 *Constants.MB)
                {
                    // kB is a reasonable unit
                    return string.Format("{0} of {1} kByte", (current /Constants.kB), (total /Constants.kB));
                }
                else if (total < 10 *Constants.GB)
                {
                    // MB is a reasonable unit
                    return string.Format("{0} of {1} MB", (current /Constants.MB), (total /Constants.MB));
                }
                else
                {
                    // GB is a reasonable unit
                    return string.Format("{0} of {1} GB", (current /Constants.GB), (total /Constants.GB));
                }
            }

            private static string relativeProgress(long current, long total)
            {
                return string.Format("{0} %",
                    (100.0 * current / total).ToString("F3"));
            }
        }

    }
}
