using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Shared
{
    public class EncodingTaskEntity : TableEntity
    {
        private TaskMetaData _taskMetaData = new TaskMetaData();
        private string _metaData;

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

        public string MetaData
        {
            get { return JsonConvert.SerializeObject(TaskMetaData); }
            set
            {
                _metaData = value;
                TaskMetaData = JsonConvert.DeserializeObject<TaskMetaData>(_metaData);
            }
        }

        public TaskMetaData TaskMetaData
        {
            get { return _taskMetaData; }
            set { _taskMetaData = value; }
        }
    }

    public class TaskMetaData
    {
        public string FfmpegLog { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string EncoderParameters { get; set; }
        public int RenditionIndex { get; set; }
    }
}
