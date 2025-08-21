# RAI - RTS AI Toolbox

Azure OpenAI-powered chatbot application with Microsoft Entra ID authentication.

## Features

- Azure OpenAI integration (GPT-4.1)
- Microsoft Entra ID SSO authentication
- Conversation logging to Azure Functions
- Responsive chat interface
- Token usage tracking
- Gradient background design (#165540 to #b4ceb3)

## Local Development

### Prerequisites

- .NET 9.0 SDK
- Azure subscription with configured services
- Microsoft Entra ID app registration

### Setup

1. Clone the repository
2. Update `appsettings.json` with your Azure configurations
3. Run the application:

```bash
dotnet build
dotnet run
```

The application will be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

## Deployment

### GitHub Actions

Push to the `net-ai-blue` branch to trigger automatic deployment to Azure App Service.

### Manual Deployment

```bash
dotnet publish -c Release -o ./publish
```

Then deploy the `./publish` folder to Azure App Service.

## Configuration

### Required Environment Variables

- `AzureOpenAI__Endpoint`: Azure OpenAI endpoint URL
- `AzureOpenAI__ApiKey`: Azure OpenAI API key
- `AzureOpenAI__DeploymentName`: Model deployment name
- `EntraId__TenantId`: Microsoft Entra tenant ID
- `EntraId__ClientId`: Application client ID
- `AzureFunction__Url`: Conversation logging function URL
- `AzureFunction__Key`: Function access key

## Architecture

- **Frontend**: Razor Pages with vanilla JavaScript
- **Backend**: ASP.NET Core 9.0
- **Authentication**: Microsoft Identity Web (MSAL)
- **AI Service**: Azure OpenAI
- **Logging**: Azure Functions
- **Deployment**: Azure App Service with blue-green slots

## Security

- All API endpoints require authentication
- Conversation history is logged securely
- API keys are stored as environment variables
- HTTPS enforced in production