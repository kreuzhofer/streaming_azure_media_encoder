#reference "tools/Cake.AzureUpload/Cake.AzureUpload.dll"

///////////////////////////////////////////////

var subscriptionId = "724467b5-bee4-484b-bf13-d6a5505d2b51";
var aadTenantId = "942023a6-efbe-4d97-a72d-532ef7337595";
var clientId = "29845b10-e037-4c15-8383-c1dc1f0949a2";
var thumbprint = "B8789A48A020FB1F5589C9ACAF63A4EBFFF5FA1C";
var artefactResourceGroup = "longterm";
var artefactStorageAccount = "chgeuerartefactswe";
var artefactStorageContainer = "artefacts";

var deploymentLocation = "northeurope";
var uniqueName = "ffmpeg2";
var deploymentResourceGroup = uniqueName;
var deploymentParameters = new Dictionary<string, object> {
    { "adminUsername", "chgeuer" }, 
    { "adminPassword", Environment.GetEnvironmentVariable("AzureVmAdminPassword") }, 
    { "deploymentName", uniqueName }, 
    { "dnsNameForPublicIP", uniqueName }, 
    { "instanceCount", 3 }, 
    { "serviceBits", string.Empty } // will be set later
};

var artefactConnectionString = string.Empty; // will be set later
var accessToken = string.Empty; // will be set later

var target = Argument("target", "Default");
var config = Argument<string>("config", "Debug");

///////////////////////////////////////////////

var outputFolder = "./output/bin/";
var mergedExecutable = "Merged.exe";

Task("Clean")
    .Does(() =>
{
    CleanDirectories(outputFolder);
    CleanDirectories("./*/bin/" + config);
    CleanDirectories("./*/obj/" + config);
});

Task("Build")
  .IsDependentOn("Clean")
  .Does(() =>
{
    Information("Default");

    NuGetRestore("./src/StreamingAzureMediaEncoder/StreamingAzureMediaEncoder.sln");
    MSBuild("./src/StreamingAzureMediaEncoder/StreamingAzureMediaEncoder.sln", settings => settings.SetConfiguration(config)); 

  CreateDirectory(outputFolder);
  ILMerge(
      outputFolder + "Agent.exe",
      "./src/StreamingAzureMediaEncoder/Agent/bin/" + config + "/" + "Agent.exe",
      GetFiles("./src/StreamingAzureMediaEncoder/Agent/bin/" + config + "/**/*.dll"));
  DeleteFile(outputFolder + "Agent.pdb");
});

Task("CopyFiles")
    .IsDependentOn("Build")
    .Does(() =>
{
    CreateDirectory(outputFolder);

    var files = GetFiles("./src/StreamingAzureMediaEncoder/Agent/bin/" + config + "/" + mergedExecutable) + 
        GetFiles("./src/StreamingAzureMediaEncoder/Agent/bin/" + config + "/ffmpeg.exe");
    CopyFiles(files, outputFolder);
});    

var zipFile = "./output/ffmpegAgent.zip";

Task("Package")
    .IsDependentOn("CopyFiles")
    .Does(() =>
{
    Zip(outputFolder, zipFile);
});

Task("AzureLogon")
    .Does(() =>
{
    accessToken = GetAzureServicePrincipalCredential(
        aadTenantId:aadTenantId, 
        clientId: clientId, 
        thumbprint: thumbprint);
});

Task("DeployAgentBits")
    .IsDependentOn("Package")
    .IsDependentOn("AzureLogon")
    .Does(() =>
{
    artefactConnectionString = GetAzureStorageAccountConnectionString(
        accessToken: accessToken, 
        subscriptionId: subscriptionId, 
		resourceGroupName:artefactResourceGroup , 
        storageAccountName: artefactStorageAccount);

    Information(string.Format("Storage: {0}", artefactConnectionString));
      
	deploymentParameters["serviceBits"] = UploadFileToAzure(
        inputFile: zipFile, 
        storageConnectionString: artefactConnectionString, 
        containerName: artefactStorageContainer);
});

Task("DeployCompute")
    .IsDependentOn("DeployAgentBits")
    .IsDependentOn("AzureLogon")
    .Does(() => { 
        try {
            AzureResourceManagerDeployment(
                subscriptionId: subscriptionId, 
                accessToken: accessToken,
                resourceGroupName: deploymentResourceGroup,
                location: deploymentLocation,
                jsonTemplateFilename: @".\src\StreamingAzureMediaEncoder\ARM\azuredeploy.json",
                parameters: deploymentParameters);
        } 
        catch (AggregateException aex) 
        {
            foreach (var inner in aex.Flatten().InnerExceptions) 
            {
                Information(inner.Message);
            } 
        }
    });

Task("Default")
  .IsDependentOn("DeployCompute");

RunTarget(target);
