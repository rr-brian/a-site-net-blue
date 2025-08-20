# PowerShell script to add production redirect URI to Entra ID app registration
# This script adds https://www.rrrealty.ai as a valid redirect URI

param(
    [Parameter(Mandatory=$false)]
    [string]$AppId = "d4c452c4-5324-40ff-b43b-25f3daa2a45c",
    
    [Parameter(Mandatory=$false)]
    [string]$TenantId = "99848873-e61d-44cc-9862-d05151c567ab",
    
    [Parameter(Mandatory=$false)]
    [string]$ProductionUrl = "https://www.rrrealty.ai"
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Cyan
$loginStatus = az account show --query name -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Yellow
    az login --tenant $TenantId
}

# Get current redirect URIs
Write-Host "Getting current redirect URIs for app $AppId..." -ForegroundColor Cyan
$app = az ad app show --id $AppId | ConvertFrom-Json
$currentUris = $app.web.redirectUris

if (-not $currentUris) {
    Write-Host "No redirect URIs found or unable to retrieve app information." -ForegroundColor Red
    Write-Host "Please ensure you have sufficient permissions to manage app registrations." -ForegroundColor Red
    exit 1
}

# Check if production URL is already in the list
if ($currentUris -contains $ProductionUrl) {
    Write-Host "Production URL $ProductionUrl is already registered as a redirect URI." -ForegroundColor Green
    exit 0
}

# Add production URL to redirect URIs
Write-Host "Adding $ProductionUrl to redirect URIs..." -ForegroundColor Cyan
$newUris = $currentUris + $ProductionUrl

# Update app registration
Write-Host "Updating app registration..." -ForegroundColor Cyan
az ad app update --id $AppId --web-redirect-uris $newUris

# Verify update
Write-Host "Verifying update..." -ForegroundColor Cyan
$updatedApp = az ad app show --id $AppId | ConvertFrom-Json
$updatedUris = $updatedApp.web.redirectUris

if ($updatedUris -contains $ProductionUrl) {
    Write-Host "Success! $ProductionUrl has been added as a redirect URI." -ForegroundColor Green
    Write-Host "Current redirect URIs:" -ForegroundColor Cyan
    $updatedUris | ForEach-Object { Write-Host "- $_" -ForegroundColor Gray }
} else {
    Write-Host "Failed to add $ProductionUrl as a redirect URI." -ForegroundColor Red
    Write-Host "Please try adding it manually in the Azure portal." -ForegroundColor Red
}
