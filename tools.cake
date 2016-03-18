var target = Argument("target", "Default");
var config = Argument<string>("config", "Debug");

Task("BuildAzureCakeTools")
  .Does(() =>
{
	Information("Building the Azure CakeBuild tool chain");

	NuGetRestore("./src/StreamingAzureMediaEncoder/Cake.AzureUpload.sln");
	MSBuild("./src/StreamingAzureMediaEncoder/Cake.AzureUpload.sln", settings => settings.SetConfiguration(config)); 

	var outputFolder = "./tools/Cake.AzureUpload";
	CreateDirectory(outputFolder);
	var files = GetFiles("./src/StreamingAzureMediaEncoder/Cake.AzureUpload/bin/" + config + "/**/*.dll");
	CopyFiles(files, outputFolder);
});

Task("Default")
  .IsDependentOn("BuildAzureCakeTools");

RunTarget(target);
