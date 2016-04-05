﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Shared;

namespace AzureChunkingMediaFileUploader
{
    public static class ProgressTracker
    {
        public static Action<string> Log { get; set; }

        internal static void log(string format, params object[] args)
        {
            if (Log != null) { Log(string.Format(format, args)); }
        }

        public static async Task TrackProgress(string jobId, string connectionString, int count)
        {
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(Constants.TableName);

            bool done = false;
            int percentageTotal = 0;
            int percentageLast = 0;
            do
            {
                TableQuery<EncodingTaskEntity> query = new TableQuery<EncodingTaskEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, jobId));

                var result = table.ExecuteQuery(query).ToList();

                percentageTotal = result.Sum(r => r.Progress)*100/(100*count);
                if (percentageTotal > percentageLast)
                {
                    log("{0}% done", percentageTotal);
                    percentageLast = percentageTotal;
                }

                if (result.Count == count && result.All(r => r.Status == Constants.STATUS_DONE || r.Status == Constants.STATUS_ABORTED))
                {
                    done = true;
                    log("All jobs done. Encoding time: {0}", result.Max(m=>m.TaskMetaData.EndTime)-result.Min(m=>m.TaskMetaData.StartTime));
                }
                await Task.Delay(1000);

            } while (done == false);
        }
    }
}