using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace Shared
{
    public class EncodingTaskEntity : TableEntity
    {
        public EncodingTaskEntity(string jobId, string taskId)
        {
            this.PartitionKey = jobId;
            this.RowKey = taskId;
        }

        public EncodingTaskEntity()
        {
            
        }

        public string Status { get; set; }
        public int Progress { get; set; }
        public string FfmpegLog { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string EncoderParameters { get; set; }
        public int RenditionIndex { get; set; }
    }
}
