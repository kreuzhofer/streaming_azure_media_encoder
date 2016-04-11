#
# deploy.ps1
#
# Deploy the ARM template with powershell and interactive login
#

Param(
	[string]$adminUsername,
	[string]$adminPassword,
	[string]$subscriptionId,
	[string]$resourceGroupName,
	[string]$deploymentId,
	[string]$targetStorageAccountResourceGroup,
	[string]$targetStorageAccountName,
	[string]$serviceBits
)

$location = "westeurope"
$instanceCount = 1;
$vmSize = "Standard_D5_v2";

# Login to Azure
Login-AzureRmAccount

# Selection subscription
Select-AzureRmSubscription -SubscriptionID $subscriptionId

# Create Resource group
New-AzureRmResourceGroup -Name $resourceGroupName -Location "$location" -Force

$parameters = @{"adminUsername"="$adminUsername"; "adminPassword" = "$adminPassword"; "deploymentId" = "$deploymentId"; "dnsNameForPublicIP" = "$deploymentId"; "instanceCount" = $instanceCount; "serviceBits" = "$serviceBits"; "targetStorageAccountResourceGroup" = "$targetStorageAccountResourceGroup"; "targetStorageAccountName" = "$targetStorageAccountName"; "vmSize" = "$vmSize" }

# Deploy ARM template
$deployResult = New-AzureRmResourceGroupDeployment -Name "$deploymentId" -Mode Complete -ResourceGroupName "$resourceGroupName" -TemplateFile azuredeploy.json -TemplateParameterObject $parameters -Force
# output result
Write-Output $deployResult

if($deployResult.ProvisioningState.Equals("Succeeded"))
{
    exit 0
}
else
{
    exit 1
}


