$queueSas = "...";
$queueUri = "https://nowtilustest.queue.core.windows.net/uploadnotifications";
$fileName = "F:\Downloads\test_kurz.ts";
$blobConnectionString = "...";
$profileFileName = "AzureChunkingMediaFileUploader\bin\Debug\profile1.json";

Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"
Start-Process .\AzureChunkingMediaEncoder\bin\Debug\AzureChunkingMediaEncoder.exe "$queueUri $queueSas"

Start-Process .\AzureChunkingMediaFileUploader\bin\Debug\AzureChunkingMediaFileUploader.exe "$fileName $blobConnectionString $profileFileName"