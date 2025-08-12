# PowerShell script to check the blue slot configuration and settings
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppServiceName,
    
    [Parameter(Mandatory=$true)]
    [string]$SlotName
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Cyan
$loginStatus = az account show --query name -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Yellow
    az login
}

# Check if the slot exists
Write-Host "Checking if slot '$SlotName' exists..." -ForegroundColor Cyan
$slotExists = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].name" -o tsv

if (-not $slotExists) {
    Write-Host "Error: Slot '$SlotName' does not exist!" -ForegroundColor Red
    exit 1
}

# Get the slot URL
$slotUrl = az webapp deployment slot list --resource-group $ResourceGroupName --name $AppServiceName --query "[?name=='$SlotName'].hostNames[0]" -o tsv
Write-Host "Slot URL: https://$slotUrl" -ForegroundColor Green

# Check the app settings for Entra ID configuration
Write-Host "Checking Entra ID settings..." -ForegroundColor Cyan
$entraIdSettings = az webapp config appsettings list --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName --query "[?starts_with(name, 'EntraId__')]" -o table

Write-Host "Entra ID Settings:" -ForegroundColor Green
Write-Output $entraIdSettings

# Check if the required settings are present
$requiredSettings = @(
    "EntraId__TenantId",
    "EntraId__ClientId",
    "EntraId__Instance",
    "EntraId__Domain",
    "EntraId__Audience",
    "EntraId__Scopes"
)

$missingSettings = @()
foreach ($setting in $requiredSettings) {
    $exists = az webapp config appsettings list --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName --query "[?name=='$setting'].name" -o tsv
    if (-not $exists) {
        $missingSettings += $setting
    }
}

if ($missingSettings.Count -gt 0) {
    Write-Host "Missing required Entra ID settings:" -ForegroundColor Red
    foreach ($setting in $missingSettings) {
        Write-Host "  - $setting" -ForegroundColor Red
    }
    
    # Provide guidance on how to set the missing settings
    Write-Host "`nTo add the missing settings, run:" -ForegroundColor Yellow
    foreach ($setting in $missingSettings) {
        Write-Host "az webapp config appsettings set --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName --settings `"$setting=YOUR_VALUE`"" -ForegroundColor Yellow
    }
} else {
    Write-Host "All required Entra ID settings are present." -ForegroundColor Green
}

# Check if the site is running
Write-Host "`nChecking if the site is running..." -ForegroundColor Cyan
$response = Invoke-WebRequest -Uri "https://$slotUrl" -UseBasicParsing -ErrorAction SilentlyContinue
if ($response.StatusCode -eq 200) {
    Write-Host "Site is running (Status: $($response.StatusCode))" -ForegroundColor Green
} else {
    Write-Host "Site might have issues (Status: $($response.StatusCode))" -ForegroundColor Red
}

# Try to access the auth config endpoint
Write-Host "`nTrying to access the auth config endpoint..." -ForegroundColor Cyan
try {
    $authResponse = Invoke-WebRequest -Uri "https://$slotUrl/api/config/auth" -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "Auth config endpoint response (Status: $($authResponse.StatusCode)):" -ForegroundColor Green
    Write-Output $authResponse.Content
} catch {
    Write-Host "Failed to access auth config endpoint: $_" -ForegroundColor Red
}

# Try to access the diagnostic endpoint
Write-Host "`nTrying to access the diagnostic endpoint..." -ForegroundColor Cyan
try {
    $diagResponse = Invoke-WebRequest -Uri "https://$slotUrl/api/config/diagnostic" -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "Diagnostic endpoint response (Status: $($diagResponse.StatusCode)):" -ForegroundColor Green
    Write-Output $diagResponse.Content
} catch {
    Write-Host "Failed to access diagnostic endpoint: $_" -ForegroundColor Red
}

# Check the deployment logs
Write-Host "`nChecking recent deployment logs..." -ForegroundColor Cyan
$deploymentLogs = az webapp log deployment show --resource-group $ResourceGroupName --name $AppServiceName --slot $SlotName
Write-Output $deploymentLogs

Write-Host "`nDiagnostic check complete!" -ForegroundColor Cyan
