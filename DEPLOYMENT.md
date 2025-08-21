# RAI Toolbox - Azure Deployment Guide

## Overview
This document describes the deployment process for the RAI Toolbox application to Azure App Service using GitHub Actions.

## Prerequisites

### GitHub Secrets Configuration
The following secrets must be configured in the GitHub repository:

1. **AZURE_WEBAPP_PUBLISH_PROFILE**
   - Download from Azure App Service → Deployment Center → Manage publish profile
   - Contains authentication information for deployment

2. **OPENAI_API_KEY**
   - Azure OpenAI API key for the generalsearchai service
   - Obtain from Azure OpenAI service configuration

3. **AZURE_CREDENTIALS** (Optional - for environment variable updates)
   - Service principal credentials for Azure CLI authentication
   - Required only if using the automated environment variable configuration step

### Azure App Service Configuration

#### Target Environment
- **App Service**: `site-net`
- **Resource Group**: `rg-innovation`
- **Deployment Slot**: `rrai-blue` (blue-green deployment)
- **Runtime**: .NET 9.0 on Windows

#### Entra ID App Registration
- **Application ID**: `d4c452c4-5324-40ff-b43b-25f3daa2a45c`
- **Tenant ID**: `99848873-e61d-44cc-9862-d05151c567ab`
- **Domain**: `rrrealty.ai`

#### Required Redirect URIs
Add these to the Entra ID app registration:
- `https://site-net.azurewebsites.net/signin-oidc` (production)
- `https://site-net-rrai-blue.azurewebsites.net/signin-oidc` (blue slot)

## Deployment Process

### Automatic Deployment
1. Push code to the `net-ai-blue` branch
2. GitHub Actions workflow will automatically:
   - Build the .NET 9.0 application
   - Publish to the Azure App Service blue slot
   - Configure environment variables
   
### Manual Deployment
1. Navigate to GitHub Actions tab
2. Select "Deploy to Azure App Service" workflow
3. Click "Run workflow" and select the `net-ai-blue` branch

### Environment Variables
The deployment automatically configures these Azure App Service settings:

```
AzureOpenAI__Endpoint=https://generalsearchai.openai.azure.com/
AzureOpenAI__ApiKey=[FROM GITHUB SECRETS]
AzureOpenAI__DeploymentName=gpt-4.1
AzureOpenAI__ApiVersion=2024-02-15-preview
EntraId__TenantId=99848873-e61d-44cc-9862-d05151c567ab
EntraId__ClientId=d4c452c4-5324-40ff-b43b-25f3daa2a45c
EntraId__Instance=https://login.microsoftonline.com/
EntraId__Domain=rrrealty.ai
AzureFunction__Url=https://fn-conversationsave.azurewebsites.net/api/conversations/update
AzureFunction__UserId=brian@rrrealty.ai
AzureFunction__UserEmail=brian@rrrealty.ai
```

## Testing the Deployment

### Blue Slot Testing
1. Access the blue slot URL: `https://site-net-rrai-blue.azurewebsites.net`
2. Verify Entra ID authentication works
3. Test document upload functionality
4. Test AI chat functionality
5. Verify conversation logging to Azure Function

### Production Promotion
After testing the blue slot:
1. Use Azure portal to swap slots (blue → production)
2. Or use PowerShell script: `swap-to-production.ps1`

## Architecture Components

### Authentication Flow
1. User accesses application
2. Redirected to Microsoft Entra ID for authentication
3. After successful authentication, user is redirected back with tokens
4. Application validates tokens and creates user session

### Document Processing
1. Files uploaded through web interface
2. Stored in local filesystem (uploads directory)
3. Text extracted using iText7 for PDFs
4. Content provided as context to AI conversations

### AI Integration
1. Azure OpenAI GPT-4.1 model for chat responses
2. Document context automatically included in prompts
3. Conversation history maintained in session
4. Responses formatted with markdown-like styling

### Conversation Logging
1. Chat history logged to Azure Function
2. Function stores data in SQL database
3. Includes user information, messages, and metadata

## Troubleshooting

### Common Issues
1. **Authentication failures**: Verify Entra ID redirect URIs
2. **OpenAI connection errors**: Check API key and endpoint configuration
3. **Document upload issues**: Verify file permissions in Azure App Service
4. **Build failures**: Ensure .NET 9.0 SDK is available

### Diagnostic Endpoints
- `/api/config` - Application configuration status
- Health checks can be added if needed

### Log Monitoring
- Azure App Service Logs
- GitHub Actions build logs
- Azure Function monitoring for conversation logging

## Security Considerations

### Secrets Management
- All sensitive configuration stored as Azure App Service settings
- GitHub secrets used for deployment credentials
- No secrets committed to source code

### Authentication
- Microsoft Entra ID single sign-on enforced
- No anonymous access allowed
- Session-based document storage (cleared on refresh)

### HTTPS
- Force HTTPS enabled in production
- Azure App Service provides SSL certificates

## Repository Structure
```
├── .github/workflows/azure-deploy.yml    # GitHub Actions deployment
├── Controllers/                          # API controllers
├── Services/                            # Business logic services
├── Pages/                               # Razor Pages
├── wwwroot/                             # Static web assets
├── appsettings.json                     # Base configuration
├── appsettings.Production.json          # Production overrides
└── Program.cs                           # Application startup
```

## Support
For deployment issues, check:
1. GitHub Actions logs for build/deployment errors
2. Azure App Service logs for runtime errors
3. Azure Function logs for conversation logging issues