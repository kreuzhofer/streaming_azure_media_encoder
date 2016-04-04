$queueSas = "?sv=2015-04-05&sig=MvNJzS%2F7fM2mzHHQNKankJhPBPqqLL1LfPVlgcKKBho%3D&se=2115-03-21T13%3A16%3A40Z&sp=rup";
$queueUri = "https://nowtilustest.queue.core.windows.net/uploadnotifications";
$fileName = "F:\Microsoft\Nowtilus\test_kurz.ts";
$blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=nowtilustest;AccountKey=2Pjon2ApHhu37pYkc4nCvp0q8xLkUOLZvsWq44ZcQWb3vuGr6FHJSwzKzISgZbKwzkhBqOToNudTlvn8wklN4w==;BlobEndpoint=https://nowtilustest.blob.core.windows.net/;TableEndpoint=https://nowtilustest.table.core.windows.net/;QueueEndpoint=https://nowtilustest.queue.core.windows.net/;FileEndpoint=https://nowtilustest.file.core.windows.net/";
$profileFileName = "AzureChunkingMediaFileUploader\bin\Debug\profile1.json";

Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"

Start-Process .\AzureChunkingMediaFileUploader\bin\Debug\AzureChunkingMediaFileUploader.exe "$fileName $blobConnectionString $profileFileName"