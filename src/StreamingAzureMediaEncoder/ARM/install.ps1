Param(
  [string]$serviceBits,
  [string]$storageAccountName,
  [string]$storageAccountKey
)

$localFile = "build.zip"
$serviceFolder = "$($env:ProgramFiles)\LocalService"

mkdir $serviceFolder
& "$($env:windir)\System32\icacls.exe" "$($serviceFolder)" /setowner '"NT AUTHORITY\NetworkService"' /T
& "$($env:windir)\System32\icacls.exe" "$($serviceFolder)" /grant    '"NT AUTHORITY\NetworkService":(OI)(M)'
& "$($env:windir)\System32\icacls.exe" "$($serviceFolder)" /grant    '"NT AUTHORITY\NetworkService":(CI)(M)'
& "$($env:windir)\System32\icacls.exe" "$($serviceFolder)" /grant    '"NT AUTHORITY\NetworkService":(OI)(CI)(M)'

Invoke-RestMethod -Uri $serviceBits -OutFile $localFile
Add-Type -Assembly "System.IO.Compression.Filesystem"
[System.IO.Compression.Zipfile]::ExtractToDirectory($localFile, $serviceFolder)

New-Item -Path HKLM:\Software\Azure\LocalService -Force
Set-ItemProperty -Path HKLM:\Software\Azure\LocalService -Name "StorageAccount" -Value "DefaultEndpointsProtocol=https;AccountName=$($storageAccountName);AccountKey=$($storageAccountKey)"

& "$($serviceFolder)\Agent.exe" install --NetworkService 
& "$($serviceFolder)\Agent.exe" start
