namespace Cake.AzureUpload
{
    using Core;
    using Core.Annotations;
    using Core.Diagnostics;
    using LargeFileUploader;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class MyCakeExtension
    {
        [CakeMethodAlias]
        public static string UploadFileToAzure(this ICakeContext context, string inputFile, string storageConnectionString, string containerName)
        {
            return context.ExecuteAsync(() =>
            {
                Action<string> cakeLog = (message) => context.Log.Write(Core.Diagnostics.Verbosity.Diagnostic, Core.Diagnostics.LogLevel.Information, message);
                LargeFileUploaderUtils.Log = cakeLog;

                return LargeFileUploaderUtils.UploadAsync(inputFile: inputFile, storageConnectionString: storageConnectionString, containerName: containerName);
            });
        }
      
        [CakeMethodAlias]
        public static string GetAzureServicePrincipalCredential(this ICakeContext context,
            string aadTenantId,
            string clientId,
            string thumbprint)
        {
            return context.ExecuteAsync(() => AzureExtensions.GetAzureServicePrincipalCredentialAsyncImpl(
                aadTenantId: aadTenantId, clientId: clientId, thumbprint: thumbprint));
        }

        [CakeMethodAlias]
        public static string GetAzureStorageAccountConnectionString(
            this ICakeContext context,
            string accessToken,
            string subscriptionId,
            string resourceGroupName,
            string storageAccountName)
        {
            return context.ExecuteAsync(() => AzureExtensions.GetAzureStorageAccountConnectionStringAsyncImpl(
                accessToken: accessToken, subscriptionId: subscriptionId,
                resourceGroupName: resourceGroupName, storageAccountName: storageAccountName));
        }

        [CakeMethodAlias]
        public static void AzureResourceManagerDeployment(
            this ICakeContext context,
            string subscriptionId, string accessToken,
            string resourceGroupName, string location,
            string jsonTemplateFilename, Dictionary<string, object> parameters)
        {
            Action<string> log = (message) => context.Log.Write(
                verbosity: Verbosity.Normal,
                level: LogLevel.Information,
                format: "{0}",
                args: message);

            context.ExecuteAsync(() => AzureExtensions.AzureResourceManagerDeploymentAsyncImpl(
                log: log,
                subscriptionId: subscriptionId,
                accessToken: accessToken,
                resourceGroupName: resourceGroupName,
                location: location,
                jsonTemplateFilename: jsonTemplateFilename,
                parameters: parameters));
        }

        private static T ExecuteAsync<T>(this ICakeContext context, Func<Task<T>> func)
        {
            try
            {
                return func().Result;
            }
            catch (AggregateException aex)
            {
                foreach (var inner in aex.Flatten().InnerExceptions)
                {
                    context.Log.Error("Error: {0}", inner.Message);
                }

                throw;
            }
        }

        private static void ExecuteAsync(this ICakeContext context, Func<Task> action)
        {
            try
            {
                action().Wait();
            }
            catch (AggregateException aex)
            {
                foreach (var inner in aex.Flatten().InnerExceptions)
                {
                    context.Log.Error("Error: {0}", inner.Message);
                }

                throw;
            }
        }
    }
}