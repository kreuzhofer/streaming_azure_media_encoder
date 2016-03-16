$queueSas = "?sv=2015-04-05&sig=2bdnp%2BJAke2E%2BCZ7ZoQuYP%2FB7bko0zPHWFD9y7aiBtA%3D&se=2016-03-17T10%3A47%3A11Z&sp=rup";
$queueUri = "https://nowtilustest.queue.core.windows.net/uploadnotifications";
$fileName = "F:\Downloads\test_kurz.ts";
$blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=nowtilustest;AccountKey=2Pjon2ApHhu37pYkc4nCvp0q8xLkUOLZvsWq44ZcQWb3vuGr6FHJSwzKzISgZbKwzkhBqOToNudTlvn8wklN4w==;BlobEndpoint=https://nowtilustest.blob.core.windows.net/;TableEndpoint=https://nowtilustest.table.core.windows.net/;QueueEndpoint=https://nowtilustest.queue.core.windows.net/;FileEndpoint=https://nowtilustest.file.core.windows.net/";
$profileFileName = "AzureChunkingMediaFileUploader\bin\Debug\profile1.json";

Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"

Start-Process .\AzureChunkingMediaFileUploader\bin\Debug\AzureChunkingMediaFileUploader.exe "$fileName $blobConnectionString $profileFileName"