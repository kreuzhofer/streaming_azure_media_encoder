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
	[string]$deploymentName,
	[string]$targetStorageAccountResourceGroup,
	[string]$targetStorageAccountName,
	[string]$instanceCount,
	[string]$serviceBits
)

$location = "northeurope"

# Login to Azure
Login-AzureRmAccount

# Selection subscription
Select-AzureRmSubscription -SubscriptionID $subscriptionId

# Create Resource group
New-AzureRmResourceGroup -Name $deploymentName -Location "$location"

$parameters = @{"adminUsername"="$adminUsername"; "adminPassword" = "$adminPassword"; "dnsNameForPublicIP" = "$deploymentName"; "instanceCount" = "$instanceCount"; "serviceBits" = "$serviceBits"; "targetStorageAccountResourceGroup" = "$targetStorageAccountResourceGroup"; "targetStorageAccountName" = "$targetStorageAccountName" }

# Deploy ARM template
New-AzureRmResourceGroupDeployment -Name "$deploymentName" -ResourceGroupName "$resourceGroupName" -TemplateFile azuredeploy.json -TemplateParameterObject $parameters


