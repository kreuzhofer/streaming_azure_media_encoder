using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;

namespace Shared
{
    public class EncodingJobEntity : TableEntity
    {
        public EncodingJobEntity(string tenantId, string jobId) : base(tenantId, jobId)
        {
        }

        public EncodingJobEntity() { }
        public string SourceFileName { get; set; }
        public string Status { get; set; }
    }
}
