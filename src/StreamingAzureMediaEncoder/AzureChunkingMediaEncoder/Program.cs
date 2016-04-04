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
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Shared;

namespace AzureChunkingMediaEncoder
{
    class Program
    {
        static void Main(string[] args)
        {
            var storageAccount = args[0];

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            var task1 = Task.Factory.StartNew(()=> { new DownloadAndEncodingTask().Start(storageAccount, ct); }, ct);
            var task2 = Task.Factory.StartNew(() => { new DownloadAndEncodingTask().Start(storageAccount, ct); }, ct);
            var task3 = Task.Factory.StartNew(() => { new DownloadAndEncodingTask().Start(storageAccount, ct); }, ct);
            var task4 = Task.Factory.StartNew(() => { new DownloadAndEncodingTask().Start(storageAccount, ct); }, ct);
            Task.WaitAll(task1, task2, task3, task4);
        }
    }
}
