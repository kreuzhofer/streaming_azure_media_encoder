// ############################################################################
// #                                                                          #
// #        ---==>  T H I S  F I L E  I S   G E N E R A T E D  <==---         #
// #                                                                          #
// # This means that any edits to the .cs file will be lost when its          #
// # regenerated. Changes should instead be applied to the corresponding      #
// # text template file (.tt)                                                 #
// ############################################################################



// ############################################################################
// @@@ INCLUDING: https://raw.githubusercontent.com/chgeuer/NLog.AzureAppendBlob/master/NLog.AzureAppendBlob/AzureAppendBlobTarget.cs
// ############################################################################
// Certains directives such as #define and // Resharper comments has to be 
// moved to top in order to work properly    
// ############################################################################
// ############################################################################

// ############################################################################
// @@@ BEGIN_INCLUDE: https://raw.githubusercontent.com/chgeuer/NLog.AzureAppendBlob/master/NLog.AzureAppendBlob/AzureAppendBlobTarget.cs
namespace NLog.AzureAppendBlob
{
    using System.Net;
    using System.Text;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using NLog.Config;
    using NLog.Layouts;
    using NLog.Targets;

    [Target("AzureAppendBlob")]
	public sealed class AzureAppendBlobTarget: TargetWithLayout
	{
		[RequiredParameter]
		public string ConnectionString { get; set; }

		[RequiredParameter]
		public string Container { get; set; }

		[RequiredParameter]
		public Layout BlobName { get; set; }

		private CloudBlobClient _client;
		private CloudBlobContainer _container;
		private CloudAppendBlob _blob;

		protected override void InitializeTarget()
		{
			base.InitializeTarget();

			_client = CloudStorageAccount.Parse(ConnectionString)
			                             .CreateCloudBlobClient();
            _container = _client.GetContainerReference(this.Container);
            try
            {
                _container.CreateIfNotExists();
            }
            catch (StorageException)
            {
                ;
            }
        }

        protected override void Write(LogEventInfo logEvent)
		{
			if (_client == null)
			{
				return;
			}

			string containerName = this.Container;
			string blobName = BlobName.Render(logEvent);

			if (_container == null || _container.Name != containerName)
			{
				_container = _client.GetContainerReference(containerName);
				_blob = null;
			}

			if (_blob == null || _blob.Name != blobName)
			{
				_blob = _container.GetAppendBlobReference(blobName);

				if (!_blob.Exists())
				{
					try
					{
						_blob.Properties.ContentType = "text/plain";
						_blob.CreateOrReplace(AccessCondition.GenerateIfNotExistsCondition());
					}
					catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == (int) HttpStatusCode.Conflict)
					{
						// to be expected
					}
				}
			}

			_blob.AppendText(Layout.Render(logEvent) + "\r\n", Encoding.UTF8);
		}
	}
}
// @@@ END_INCLUDE: https://raw.githubusercontent.com/chgeuer/NLog.AzureAppendBlob/master/NLog.AzureAppendBlob/AzureAppendBlobTarget.cs
// ############################################################################
// ############################################################################
// Certains directives such as #define and // Resharper comments has to be 
// moved to bottom in order to work properly    
// ############################################################################
// ############################################################################
namespace notinuse.Include
{
    static partial class MetaData
    {
        public const string RootPath        = @"";
        public const string IncludeDate     = @"2016-03-15T16:47:42";

        public const string Include_0       = @"https://raw.githubusercontent.com/chgeuer/NLog.AzureAppendBlob/master/NLog.AzureAppendBlob/AzureAppendBlobTarget.cs";
    }
}
// ############################################################################


