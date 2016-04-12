using System;
using Microsoft.Practices.TransientFaultHandling;

namespace AzureUploaderGui
{
    public class HttpErrorStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            return true; // lazy, I know
        }
    }
}