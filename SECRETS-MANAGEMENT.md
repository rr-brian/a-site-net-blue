# Secrets Management Guide

This document outlines best practices for managing secrets and sensitive configuration in the AI Document Chat application.

## Environment Variables

The application is configured to use environment variables for sensitive information. The following environment variables are supported:

### Azure Function Configuration
- `AZURE_FUNCTION_URL` - URL endpoint for the conversation save function
- `AZURE_FUNCTION_KEY` - Authentication key for the conversation save function
- `AZURE_FUNCTION_USER_ID` - Optional user ID for conversation tracking
- `AZURE_FUNCTION_USER_EMAIL` - Optional user email for conversation tracking

### Azure OpenAI Configuration
- `OPENAI_API_KEY` - API key for Azure OpenAI
- `OPENAI_ENDPOINT` - Endpoint URL for Azure OpenAI
- `OPENAI_DEPLOYMENT_NAME` - Deployment name (defaults to "gpt-35-turbo" if not specified)

### Entra ID (Azure AD) Configuration
- `ENTRA_ID_TENANT_ID` - Tenant ID for Entra ID authentication
- `ENTRA_ID_CLIENT_ID` - Client ID for Entra ID authentication

## Configuration Precedence

The application follows this precedence order for configuration:
1. Environment variables (highest priority)
2. App Service Configuration settings (when deployed to Azure)
3. `appsettings.json` values (lowest priority)

## Local Development

For local development:

1. Create a copy of `appsettings.template.json` and name it `appsettings.Development.json`
2. Add your development configuration values
3. This file is excluded from Git via `.gitignore`

## Azure Deployment

When deploying to Azure:

1. Configure all sensitive values as App Service Configuration settings
2. Use slot-specific settings for blue/green deployment scenarios
3. Never commit actual secrets to the Git repository

## Git Repository Guidelines

The following files are excluded from Git to prevent accidental exposure of secrets:
- `appsettings.Development.json`
- `appsettings.json`

Always use the template files as a reference and configure actual secrets through environment variables or Azure App Service Configuration.
