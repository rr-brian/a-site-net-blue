# PowerShell script to swap the blue slot to production
# This script performs a slot swap operation to make the blue slot live in production

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

# Confirm before proceeding
Write-Host "WARNING: This will swap the '$SourceSlotName' slot to production." -ForegroundColor Yellow
Write-Host "All changes in the blue slot will become live in production." -ForegroundColor Yellow
$confirmation = Read-Host "Are you sure you want to proceed? (y/n)"
if ($confirmation -ne 'y') {
    Write-Host "Operation cancelled." -ForegroundColor Red
    exit 0
}

# Perform the slot swap
Write-Host "Swapping slot '$SourceSlotName' to production..." -ForegroundColor Cyan
az webapp deployment slot swap --resource-group $ResourceGroupName --name $AppServiceName --slot $SourceSlotName --target-slot production

# Check if the swap was successful
if ($LASTEXITCODE -eq 0) {
    Write-Host "Slot swap completed successfully!" -ForegroundColor Green
    Write-Host "The changes are now live in production." -ForegroundColor Green
    
    # Get the production URL
    $productionUrl = az webapp show --resource-group $ResourceGroupName --name $AppServiceName --query defaultHostName -o tsv
    Write-Host "Production URL: https://$productionUrl" -ForegroundColor Cyan
} else {
    Write-Host "Error: Slot swap failed." -ForegroundColor Red
    exit 1
}
