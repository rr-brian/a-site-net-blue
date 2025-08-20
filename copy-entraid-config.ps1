# PowerShell script to copy Entra ID configuration from blue slot to production
# This ensures authentication will work after slot swap

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-innovation",
    
    [Parameter(Mandatory=$false)]
    [string]$AppServiceName = "site-net",
    
    [Parameter(Mandatory=$false)]
    [string]$SourceSlotName = "rrai-blue"
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Cyan
$loginStatus = az account show --query name -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Yellow
    az login
}

# Get Entra ID settings from blue slot
Write-Host "Getting Entra ID settings from $SourceSlotName slot..." -ForegroundColor Cyan
$entraIdSettings = az webapp config appsettings list --resource-group $ResourceGroupName --name $AppServiceName --slot $SourceSlotName --query "[?contains(name, 'EntraId')].{Name:name, Value:value}" -o json | ConvertFrom-Json

if (-not $entraIdSettings -or $entraIdSettings.Count -eq 0) {
    Write-Host "No Entra ID settings found in $SourceSlotName slot." -ForegroundColor Red
    exit 1
}

# Apply settings to production slot
Write-Host "Applying Entra ID settings to production slot..." -ForegroundColor Cyan
foreach ($setting in $entraIdSettings) {
    Write-Host "Setting $($setting.Name)..." -ForegroundColor Gray
    az webapp config appsettings set --resource-group $ResourceGroupName --name $AppServiceName --settings "$($setting.Name)=$($setting.Value)" | Out-Null
}

Write-Host "Entra ID configuration copied successfully to production slot!" -ForegroundColor Green
Write-Host "You can now safely perform a slot swap." -ForegroundColor Cyan
