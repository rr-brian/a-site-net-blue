# Azure Deployment Guide - AI Document Chat with Entra ID

This guide outlines the steps to deploy the AI Document Chat application to Azure with Entra ID authentication and blue/green deployment support.

## Prerequisites

1. **Azure Subscription** with appropriate permissions
2. **Azure CLI** installed and configured
3. **Entra ID (Azure AD) Tenant** with admin access
4. **Azure OpenAI Service** provisioned

## Step 1: Configure Entra ID Application

### 1.1 Register Application in Azure AD

```bash
# Create the app registration
az ad app create \
  --display-name "AI Document Chat" \
  --web-redirect-uris "https://your-app-name.azurewebsites.net" \
  --required-resource-accesses @app-manifest.json
```

### 1.2 Configure API Permissions

1. Navigate to **Azure Portal > Azure Active Directory > App registrations**
2. Select your application
3. Go to **API permissions**
4. Add the following permissions:
   - Microsoft Graph: `User.Read` (Delegated)
   - Microsoft Graph: `profile` (Delegated)
   - Microsoft Graph: `openid` (Delegated)

### 1.3 Configure Authentication

1. Go to **Authentication** section
2. Add platform: **Single-page application**
3. Add redirect URI: `https://your-app-name.azurewebsites.net`
4. Enable **Access tokens** and **ID tokens**
5. Set **Supported account types** to your organization

### 1.4 Expose an API (Optional for API-to-API calls)

1. Go to **Expose an API**
2. Set Application ID URI: `api://your-client-id`
3. Add scope: `access_as_user`

## Step 2: Update Configuration Files

### 2.1 Update appsettings.json

```json
{
  "EntraId": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.com",
    "Audience": "api://your-client-id"
  }
}
```

### 2.2 Update Frontend Configuration

Update the MSAL configuration in `wwwroot/js/auth.js`:

```javascript
const msalConfig = {
    auth: {
        clientId: 'your-client-id',
        authority: 'https://login.microsoftonline.com/your-tenant-id',
        redirectUri: window.location.origin
    }
};

const apiRequest = {
    scopes: ['api://your-client-id/access_as_user']
};
```

## Step 3: Deploy Infrastructure

### 3.1 Deploy Azure Resources

```bash
# Create resource group
az group create --name rg-ai-chat-prod --location eastus

# Deploy ARM template
az deployment group create \
  --resource-group rg-ai-chat-prod \
  --template-file deployment/azure-deployment.json \
  --parameters \
    appName="ai-document-chat" \
    environment="prod" \
    entraIdTenantId="your-tenant-id" \
    entraIdClientId="your-client-id" \
    openAiEndpoint="https://your-openai.openai.azure.com/" \
    openAiApiKey="your-openai-key"
```

### 3.2 Configure App Service Settings

```bash
# Set additional configuration
az webapp config appsettings set \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "EntraId__Domain=your-domain.com" \
    "EntraId__Audience=api://your-client-id"
```

## Step 4: Blue/Green Deployment Process

### 4.1 Deploy to Blue Slot (Staging)

```bash
# Build and publish the application
dotnet publish -c Release -o ./publish

# Deploy to blue slot
az webapp deployment source config-zip \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --slot blue \
  --src ./publish.zip
```

### 4.2 Test Blue Slot

1. Access the blue slot URL: `https://ai-document-chat-prod-blue.azurewebsites.net`
2. Test authentication flow
3. Test document upload and chat functionality
4. Verify all API endpoints work correctly

### 4.3 Swap Slots (Go Live)

```bash
# Swap blue slot to production
az webapp deployment slot swap \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --slot blue \
  --target-slot production
```

### 4.4 Rollback (if needed)

```bash
# Rollback by swapping back
az webapp deployment slot swap \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --slot production \
  --target-slot blue
```

## Step 5: Configure CI/CD Pipeline

### 5.1 GitHub Actions Workflow

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: '9.0.x'
    
    - name: Build
      run: dotnet build --configuration Release
    
    - name: Publish
      run: dotnet publish -c Release -o ./publish
    
    - name: Deploy to Blue Slot
      uses: azure/webapps-deploy@v2
      with:
        app-name: 'ai-document-chat-prod'
        slot-name: 'blue'
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: './publish'
    
    - name: Swap Slots
      run: |
        az webapp deployment slot swap \
          --resource-group rg-ai-chat-prod \
          --name ai-document-chat-prod \
          --slot blue \
          --target-slot production
```

## Step 6: Security Configuration

### 6.1 Enable HTTPS Only

```bash
az webapp update \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --https-only true
```

### 6.2 Configure CORS (if needed)

```bash
az webapp cors add \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --allowed-origins "https://your-domain.com"
```

### 6.3 Enable Application Insights

```bash
az monitor app-insights component create \
  --app ai-document-chat-insights \
  --location eastus \
  --resource-group rg-ai-chat-prod
```

## Step 7: Monitoring and Logging

### 7.1 Configure Log Analytics

```bash
# Enable application logging
az webapp log config \
  --resource-group rg-ai-chat-prod \
  --name ai-document-chat-prod \
  --application-logging filesystem \
  --level information
```

### 7.2 Set Up Alerts

Configure alerts for:
- Application errors
- High response times
- Authentication failures
- Resource utilization

## Troubleshooting

### Common Issues

1. **Authentication Redirect Loop**
   - Verify redirect URIs match exactly
   - Check MSAL configuration

2. **API Authorization Failures**
   - Verify JWT token validation settings
   - Check audience and issuer configuration

3. **CORS Issues**
   - Configure appropriate CORS policies
   - Verify origin URLs

### Useful Commands

```bash
# View application logs
az webapp log tail --resource-group rg-ai-chat-prod --name ai-document-chat-prod

# Restart application
az webapp restart --resource-group rg-ai-chat-prod --name ai-document-chat-prod

# Check deployment status
az webapp deployment list --resource-group rg-ai-chat-prod --name ai-document-chat-prod
```

## Configuration Checklist

- [ ] Entra ID application registered
- [ ] API permissions configured
- [ ] Authentication settings updated
- [ ] Azure resources deployed
- [ ] App settings configured
- [ ] Blue/green slots created
- [ ] HTTPS enabled
- [ ] Monitoring configured
- [ ] CI/CD pipeline set up
- [ ] Security policies applied

## Support

For issues with this deployment:
1. Check Azure App Service logs
2. Verify Entra ID configuration
3. Test authentication flow in browser dev tools
4. Review Application Insights telemetry
