# PowerShell script to redeploy the application to the blue slot using Azure CLI
# This script builds the application and deploys it to the blue slot

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-innovation",
    
    [Parameter(Mandatory=$false)]
    [string]$AppServiceName = "site-net",
    
    [Parameter(Mandatory=$false)]
    [string]$SlotName = "rrai-blue"
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Cyan
$loginStatus = az account show --query name -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Yellow
    az login
}

# Build the application
Write-Host "Building the application..." -ForegroundColor Cyan
$currentDir = Get-Location
Set-Location $PSScriptRoot

# Check if dotnet is installed
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK not found. Please install .NET SDK." -ForegroundColor Red
    exit 1
}

# Build and publish the application
Write-Host "Publishing the application..." -ForegroundColor Cyan
dotnet publish -c Release -o ./publish/app

# Check if the publish was successful
if (-not (Test-Path "./publish/app")) {
    Write-Host "Error: Failed to publish the application." -ForegroundColor Red
    exit 1
}

# Create a ZIP file for deployment
Write-Host "Creating deployment package..." -ForegroundColor Cyan
$zipPath = "./publish/app.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory("./publish/app", $zipPath)

# Check if the specified slot exists
Write-Host "Checking if slot '$SlotName' exists..." -ForegroundColor Cyan
$slotExists = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].name" -o tsv

if (-not $slotExists) {
    Write-Host "Slot '$SlotName' does not exist. Creating it now..." -ForegroundColor Yellow
    az webapp deployment slot create --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName
    Write-Host "Slot '$SlotName' created." -ForegroundColor Green
}

# Deploy to the specified slot
Write-Host "Deploying to slot '$SlotName'..." -ForegroundColor Cyan
az webapp deployment source config-zip --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName --src $zipPath

# Restart the slot
Write-Host "Restarting slot '$SlotName'..." -ForegroundColor Cyan
az webapp restart --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName

# Get the slot URL
$slotUrl = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].hostNames[0]" -o tsv

Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host "Slot URL: https://$slotUrl" -ForegroundColor Cyan

# Clean up
Write-Host "Cleaning up..." -ForegroundColor Cyan
Set-Location $currentDir
