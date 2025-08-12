# PowerShell script to help configure Entra ID authentication
# Run this script after creating your Azure AD app registration

param(
    [Parameter(Mandatory=$true)]
    [string]$ClientId,
    
    [Parameter(Mandatory=$true)]
    [string]$TenantId,
    
    [Parameter(Mandatory=$false)]
    [string]$Domain = ""
)

Write-Host "Setting up Entra ID authentication configuration..." -ForegroundColor Green

# Update appsettings.Development.json
$devConfigPath = "Backend\appsettings.Development.json"
$templatePath = "Backend\appsettings.Development.template.json"

if (-not (Test-Path $devConfigPath)) {
    if (Test-Path $templatePath) {
        Write-Host "Creating appsettings.Development.json from template..." -ForegroundColor Yellow
        Copy-Item $templatePath $devConfigPath
    } else {
        Write-Host "Creating new appsettings.Development.json..." -ForegroundColor Yellow
        $devConfig = @{
            "Logging" = @{
                "LogLevel" = @{
                    "Default" = "Information"
                    "Microsoft.AspNetCore" = "Warning"
                    "Backend.Controllers" = "Debug"
                    "Microsoft.AspNetCore.Authentication" = "Debug"
                }
            }
            "AllowedHosts" = "*"
            "EntraId" = @{
                "TenantId" = $TenantId
                "ClientId" = $ClientId
                "Instance" = "https://login.microsoftonline.com/"
                "Domain" = $Domain
                "Audience" = "api://$ClientId"
                "Scopes" = @("openid", "profile", "email")
            }
            "AzureOpenAI" = @{
                "ApiKey" = ""
                "Endpoint" = ""
                "DeploymentName" = "gpt-4.1"
                "ApiVersion" = "2025-01-01-preview"
            }
            "AzureFunction" = @{
                "Url" = "https://fn-conversationsave.azurewebsites.net/api/conversations/update"
                "Key" = ""
            }
        }
        $devConfig | ConvertTo-Json -Depth 10 | Set-Content $devConfigPath
    }
}

# Update the configuration values in the existing file
$config = Get-Content $devConfigPath | ConvertFrom-Json
$config.EntraId.TenantId = $TenantId
$config.EntraId.ClientId = $ClientId
$config.EntraId.Audience = "api://$ClientId"
if ($Domain) {
    $config.EntraId.Domain = $Domain
}

$config | ConvertTo-Json -Depth 10 | Set-Content $devConfigPath
Write-Host "✓ Updated appsettings.Development.json" -ForegroundColor Green

# Update auth.js
$authJsPath = "Backend\wwwroot\js\auth.js"
if (Test-Path $authJsPath) {
    $authContent = Get-Content $authJsPath -Raw
    $authContent = $authContent -replace "YOUR_CLIENT_ID_HERE", $ClientId
    $authContent = $authContent -replace "YOUR_TENANT_ID_HERE", $TenantId
    Set-Content $authJsPath $authContent
    Write-Host "✓ Updated auth.js configuration" -ForegroundColor Green
} else {
    Write-Host "⚠ auth.js not found at $authJsPath" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Configuration complete! Next steps:" -ForegroundColor Cyan
Write-Host "1. Make sure your Azure AD app registration has these redirect URIs:" -ForegroundColor White
Write-Host "   - http://localhost:5239" -ForegroundColor Gray
Write-Host "   - https://localhost:7175" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Run the application:" -ForegroundColor White
Write-Host "   cd Backend" -ForegroundColor Gray
Write-Host "   dotnet run --urls `"http://localhost:5239;https://localhost:7175`"" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Open browser to: https://localhost:7175" -ForegroundColor White
Write-Host ""
Write-Host "Your configuration:" -ForegroundColor Cyan
Write-Host "Client ID: $ClientId" -ForegroundColor Gray
Write-Host "Tenant ID: $TenantId" -ForegroundColor Gray
Write-Host "Authority: https://login.microsoftonline.com/$TenantId" -ForegroundColor Gray
