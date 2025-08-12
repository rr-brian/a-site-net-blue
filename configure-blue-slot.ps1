# PowerShell script to configure blue slot settings in Azure App Service
# This script updates app settings for the blue slot without affecting production

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppServiceName,
    
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

# Check if the blue slot exists
Write-Host "Checking if blue slot exists..." -ForegroundColor Cyan
$slotExists = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='blue'].name" -o tsv

if (-not $slotExists) {
    Write-Host "Blue slot does not exist. Creating it now..." -ForegroundColor Yellow
    az webapp deployment slot create --resource-group $ResourceGroupName --name $AppServiceName --slot blue
    Write-Host "Blue slot created." -ForegroundColor Green
}

# Update app settings for the blue slot
Write-Host "Updating app settings for the blue slot..." -ForegroundColor Cyan

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

az webapp config appsettings set --resource-group $ResourceGroupName --name $AppServiceName --slot blue --settings $settings

Write-Host "Blue slot configuration complete!" -ForegroundColor Green
Write-Host "Blue slot URL: https://$AppServiceName-blue.azurewebsites.net" -ForegroundColor Cyan
Write-Host "Test the blue slot before swapping to production." -ForegroundColor Yellow

# Display a reminder about Entra ID app registration
Write-Host "`nIMPORTANT REMINDER:" -ForegroundColor Red
Write-Host "Ensure your Entra ID app registration has these redirect URIs:" -ForegroundColor Yellow
Write-Host "- https://$AppServiceName.azurewebsites.net/" -ForegroundColor White
Write-Host "- https://$AppServiceName-blue.azurewebsites.net/" -ForegroundColor White
Write-Host "- Any custom domain URLs" -ForegroundColor White
