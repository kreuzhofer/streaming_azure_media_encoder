namespace Cake.AzureUpload
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Microsoft.Azure.Management.Resources;
    using Microsoft.Azure.Management.Resources.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;

    internal static class AzureExtensions
    {
        public static async Task AzureResourceManagerDeploymentAsyncImpl(
            Action<string> log,
            string subscriptionId, string accessToken,
            string resourceGroupName, string location,
            string jsonTemplateFilename, Dictionary<string, object> parameters)
        {
            log = log.WrapLog();

            log($"Starting an Azure Deploy");
            var resourceManagementClient = new ResourceManagementClient(new TokenCredentials(token: accessToken)) { SubscriptionId = subscriptionId };
            var rgResult = await resourceManagementClient.ResourceGroups.CreateOrUpdateAsync(
                resourceGroupName: resourceGroupName,
                parameters: new ResourceGroup { Location = location });
            log($"Created resource group {rgResult.Name}: {rgResult.Properties.ProvisioningState}");

            log("Trigger deployment");

            Func<Dictionary<string, object>, dynamic> getARMParameterObject = (p) =>
            {
                Func<object, JObject> val = (value) => JObject.Parse($"{{'value': {   JsonConvert.SerializeObject(new JValue(value))  } }}");
                dynamic jsonObject = JObject.Parse(@"{ }");
                p.ToList().ForEach(kvp => ((JObject)jsonObject).Add(kvp.Key, val(kvp.Value)));
                return jsonObject;
            };

            var templateJsonString = File.ReadAllText(jsonTemplateFilename);
            var parametersObject = getARMParameterObject(parameters);
            var deployResult = await resourceManagementClient.Deployments.CreateOrUpdateWithHttpMessagesAsync(
                resourceGroupName: resourceGroupName,
                deploymentName: $"{resourceGroupName}-{DateTime.UtcNow.ToString("yyyy-MM-dd--hh-mm")}",
                parameters: new Deployment
                {
                    Properties = new DeploymentProperties
                    {
                        Mode = DeploymentMode.Incremental,
                        Template = JsonConvert.DeserializeObject<dynamic>(templateJsonString),
                        Parameters = parametersObject
                    }
                });

            log($"Deployment {deployResult.Body.Name} has status {deployResult.Response.StatusCode}");
        }

        // public static async Task<string> CreateAzureContainer(string storageConnectionString, string containerName)

        public static async Task<string> GetAzureStorageAccountConnectionStringAsyncImpl(
            string accessToken, string subscriptionId,
            string resourceGroupName, string storageAccountName)
        {
            var apiVersion = "2015-05-01-preview";
            var postUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}/listKeys?api-version={apiVersion}";

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, postUrl);
            request.Headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {accessToken}");
            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var body = JsonConvert.DeserializeObject<dynamic>(json);

            return $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={body.key1}";
        }

        internal static Tuple<byte[], string> FromStore(string thumbprint)
        {
            var store = new X509Store(storeName: StoreName.My, storeLocation: StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var c = store.Certificates.Find(
                        findType: X509FindType.FindByThumbprint,
                        findValue: thumbprint,
                        validOnly: false)[0];

                var pass = Guid.NewGuid().ToString();
                return Tuple.Create(c.Export(X509ContentType.Pkcs12, pass), pass);
            }
            catch
            {
                try
                {
                    if (store != null)
                    {
                        store.Close();
                    }
                }
                catch (Exception)
                {
                    ;
                }

                throw;
            }
        }

        public static async Task<string> GetAzureServicePrincipalCredentialAsyncImpl(
          string aadTenantId,
          string clientId,
          string thumbprint)
        {
            const string AzureManagementPortal = "https://management.core.windows.net/";
            var authenticationContext = new AuthenticationContext($"https://login.microsoftonline.com/{aadTenantId}");
            var cert = FromStore(thumbprint);

            var certCred = new ClientAssertionCertificate(
                clientId: clientId,
                certificate: cert.Item1,
                password: cert.Item2);
            var token = await authenticationContext.AcquireTokenAsync(
                resource: AzureManagementPortal,
                clientCertificate: certCred);

            return token.AccessToken;
        }

        public static Action<string> WrapLog(this Action<string> log)
        {
            return log != null ? log : (s) => Console.WriteLine("{0}", s);
        }
    }
}
