# PowerShell script to configure blue slot settings in Azure App Service
# This script updates app settings for the blue slot without affecting production

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-innovation",
    
    [Parameter(Mandatory=$false)]
    [string]$AppServiceName = "site-net",
    
    [Parameter(Mandatory=$false)]
    [string]$SlotName = "rrai-blue",
    
    [Parameter(Mandatory=$true)]
    [string]$TenantId,
    
    [Parameter(Mandatory=$true)]
    [string]$ApiClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$TenantDomain,
    
    [Parameter(Mandatory=$true)]
    [string]$OpenAIEndpoint,
    
    [Parameter(Mandatory=$true)]
    [string]$OpenAIKey,
    
    [Parameter(Mandatory=$true)]
    [string]$FunctionUrl,
    
    [Parameter(Mandatory=$true)]
    [string]$FunctionKey,
    
    [Parameter(Mandatory=$false)]
    [string]$OpenAIDeploymentName = "gpt-35-turbo"
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Cyan
$loginStatus = az account show --query name -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Yellow
    az login
}

# Check if the specified slot exists
Write-Host "Checking if slot '$SlotName' exists..." -ForegroundColor Cyan
$slotExists = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].name" -o tsv

if (-not $slotExists) {
    Write-Host "Slot '$SlotName' does not exist. Creating it now..." -ForegroundColor Yellow
    az webapp deployment slot create --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName
    Write-Host "Slot '$SlotName' created." -ForegroundColor Green
}

# Update app settings for the specified slot
Write-Host "Updating app settings for the slot '$SlotName'..." -ForegroundColor Cyan

$settings = @(
    "EntraId__TenantId=$TenantId",
    "EntraId__ClientId=$ApiClientId",
    "EntraId__Instance=https://login.microsoftonline.com/",
    "EntraId__Domain=$TenantDomain",
    "EntraId__Audience=api://$ApiClientId",
    "EntraId__Scopes=api://$ApiClientId/access_as_user",
    "AzureOpenAI__Endpoint=$OpenAIEndpoint",
    "AzureOpenAI__ApiKey=$OpenAIKey",
    "AzureOpenAI__DeploymentName=$OpenAIDeploymentName",
    "AzureFunction__Url=$FunctionUrl",
    "AzureFunction__Key=$FunctionKey"
)

# Apply settings to the slot
Write-Host "Applying settings to slot '$SlotName'..." -ForegroundColor Cyan
az webapp config appsettings set --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName --settings $settings

# Restart the slot to apply changes
Write-Host "Restarting slot '$SlotName'..." -ForegroundColor Cyan
az webapp restart --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName

# Get the slot URL
$slotUrl = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].hostNames[0]" -o tsv

Write-Host "Configuration complete!" -ForegroundColor Green
Write-Host "Slot URL: https://$slotUrl" -ForegroundColor Cyan
Write-Host "Blue slot URL: https://$AppServiceName-blue.azurewebsites.net" -ForegroundColor Cyan
Write-Host "Test the blue slot before swapping to production." -ForegroundColor Yellow

# Display a reminder about Entra ID app registration
Write-Host "`nIMPORTANT REMINDER:" -ForegroundColor Red
Write-Host "Ensure your Entra ID app registration has these redirect URIs:" -ForegroundColor Yellow
Write-Host "- https://$AppServiceName.azurewebsites.net/" -ForegroundColor White
Write-Host "- https://$AppServiceName-blue.azurewebsites.net/" -ForegroundColor White
Write-Host "- Any custom domain URLs" -ForegroundColor White
