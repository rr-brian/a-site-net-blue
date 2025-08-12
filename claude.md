# ASP.NET Core AI Chatbot Project Scope

## Project Overview

This document outlines the scope, architecture, and development roadmap for the ASP.NET Core 9.0 AI Chatbot web application.

## Technical Stack

- **Backend**: ASP.NET Core 9.0
- **Frontend**: Modularized JavaScript (ES modules)
- **API Integration**: Azure OpenAI API, Azure Functions
- **Deployment Target**: Azure App Service with blue-green deployment slots
- **UI Components**: Font Awesome 6.0.0-beta3 icons, custom CSS

## Core Features

1. **AI Chat Interface**
   - Real-time conversation with AI assistants
   - Message history with timestamps
   - Code block formatting and syntax highlighting
   - Smooth scrolling behavior
   - Typing indicators

2. **Document Processing**
   - File upload system with visual indicators
   - Support for PDF, DOCX, and XLSX formats
   - Text extraction and chunking for large documents
   - Context-aware document queries
   - Page-specific search capability

3. **User Experience Enhancements**
   - File upload indicators showing file icon, name, size, type
   - Visual feedback when files are attached
   - Badge counter for attached files
   - Smooth animations and transitions
   - Responsive design for various devices

## Architecture Components

1. **Frontend Modules**
   - `ui.js`: UI rendering, formatting, and animations
   - `chat.js`: Message handling, API calls, and history management
   - `document-handler.js`: File upload and document processing
   - `script.js`: Main application logic and event coordination

2. **Backend Services**
   - **Controllers**:
     - `ChatController`: Handles core chat functionality without document context
     - `DocumentChatController`: Handles document upload, document-based chat, and document context clearing
     - `ConfigController`: Manages configuration and diagnostic endpoints
   - **Services**:
     - `ChatService`: Processes chat requests and integrates with Azure OpenAI
     - `DocumentProcessingService`: Text extraction from various file types
     - `DocumentChunkingService`: Breaks documents into manageable chunks
     - `DocumentPersistenceService`: Stores document context between sessions
     - `DocumentContextService`: Prepares document context for inclusion in chat prompts
     - `PromptEngineeringService`: Creates effective system prompts
     - `AzureFunctionService`: Integration with Azure Functions

3. **External Dependencies**
   - Azure OpenAI for natural language processing
   - Azure Functions for stateless processing tasks
   - Azure App Service for hosting
   - Azure Storage for document and conversation history

## Current Development Status

### Completed Items

- Basic chat interface with AI integration
- File upload functionality
- Document text extraction for PDF, Word, and Excel
- Document chunking for context management
- Enhanced UI components and animations
- Controller refactoring for improved separation of concerns:
  - Split monolithic ChatController into three specialized controllers
  - Improved document persistence across sessions
  - Fixed document context loss between chat requests
- Complete system architecture documentation (`system-architecture.md`)

### In-Progress Items

- Improving document chunking algorithm for better recall
- Enhancing semantic search for finding relevant content
- Adding better page identification in PDF documents
- Optimizing the UI for long messages and large documents

### Planned Enhancements

1. **Document Search Improvements**
   - Implement keyword-based relevance scoring for document chunks
   - Add support for page-specific queries in large documents
   - Increase number of chunks used in context for better recall
   - Improve text extraction quality from tabular data

2. **UI Refinements**
   - Add better visual feedback during document processing
   - Implement progressive loading for long conversations
   - Enhance error messaging and recovery
   - Add accessibility improvements

3. **Performance Optimizations**
   - Optimize large document handling
   - Implement caching for frequently accessed content
   - Add background processing for document indexing
   - Optimize Azure Function calls and error handling

## Coding Guidelines

### General Principles

1. **Generalized Solutions**
   - Always implement solutions that work for all users and use cases
   - Avoid client-specific code that only addresses particular organizations or entities
   - Create flexible, configurable systems rather than hardcoding specific scenarios
   - Never add special-case handling for specific companies, organizations, or entities (like "ITA Group")
   - Design features that are robust and work with any uploaded document or user query

2. **Code Quality Standards**
   - Follow SOLID principles in all new code
   - Write self-documenting code with clear naming conventions
   - Add appropriate comments for complex logic
   - Ensure all features are accessible for all users
   - Implement proper error handling for edge cases

3. **Security and Performance**
   - Always follow security best practices
   - Optimize for performance with large documents
   - Consider memory usage when processing large files
   - Implement appropriate timeouts and resource limits

## Build and Deployment

### Local Development

```
dotnet build
dotnet run --urls "http://localhost:5239"
```

### Application URLs and Ports

- **HTTP**: http://localhost:5239
- **HTTPS**: https://localhost:7175

> **Note**: Always store these configuration details automatically in this document for future reference. Port configurations are defined in `Backend/Properties/launchSettings.json`.

### Deployment Process

1. Build the application in Release mode
2. Deploy to staging slot in Azure App Service
3. Run automated tests
4. Swap staging and production slots

### Environment Configuration

- Azure Function URLs and keys are configured via environment variables
- Sensitive keys are managed outside source control
- Configuration is environment-specific (dev/staging/prod)

## Debugging and Testing

- Logging is implemented at multiple levels of the application
- Error handling includes detailed information for troubleshooting
- Local debugging is configured through Visual Studio or dotnet CLI
- Unit tests cover core functionality

### Azure OpenAI Integration

#### Configuration and Deployment Issues

- **Fixed Deployment Name Issue**: Resolved 404 DeploymentNotFound errors by ensuring the application consistently uses "gpt-4.1" deployment name
  - Created a DiagnosticController with endpoints to view OpenAI configuration and test deployments
  - Diagnostic testing revealed only "gpt-4.1" deployment exists in the Azure OpenAI resource
  - Modified OpenAIConfiguration.cs to override any incorrect deployment names with "gpt-4.1"
  - Updated ChatService.cs to use the correct deployment name
  - Added environment variable validation to ensure OPENAI_DEPLOYMENT_NAME (if set) contains valid value

#### API Version Information

- Azure OpenAI API version: 2025-01-01-preview
- Client type: Azure.AI.OpenAI.OpenAIClient
- Endpoint: https://generalsearchai.openai.azure.com/

### Azure Function Integration

#### Configuration

- Successfully integrated Azure Function for saving conversations
- Endpoint: https://fn-conversationsave.azurewebsites.net/api/conversations/update
- Required environment variables or appsettings.json configuration:
  - `AzureFunction:Url` or `AZURE_FUNCTION_URL`: The Azure Function endpoint URL
  - `AzureFunction:Key` or `AZURE_FUNCTION_KEY`: The Azure Function access key
  - `AzureFunction:UserId` or `AZURE_FUNCTION_USER_ID`: Optional user ID (auto-generated if not provided)
  - `AzureFunction:UserEmail` or `AZURE_FUNCTION_USER_EMAIL`: Optional user email (auto-generated if not provided)

#### Implementation Details

- Added IAzureFunctionService interface and AzureFunctionService implementation
- ChatService asynchronously calls SaveConversationAsync after successful chat completions
- Implemented fire-and-forget pattern with proper error handling to prevent affecting main application flow
- Added diagnostic endpoints to test Azure Function configuration and connectivity
- Conversation data format:
  ```json
  {
    "userId": "string",
    "userEmail": "string",
    "chatType": "web",
    "messages": [
      { "role": "user", "content": "string" },
      { "role": "assistant", "content": "string" }
    ],
    "totalTokens": 0,
    "metadata": {
      "source": "web",
      "timestamp": "2025-07-16T20:22:35.7479914Z"
    }
  }
  ```

#### Important Notes

- The Azure Function is designed to create a new record for each conversation if no conversationId is provided
- The conversationId field is intentionally omitted from the request to trigger the "create new" flow
- The Azure Function returns the generated conversationId in the response
- The function connects to SQL Server database `rts-sql-main` on server `rts-sql-main.database.windows.net`
- Session state management:
  - Document context is automatically cleared when the user closes the application
  - This prevents document context from persisting between user sessions unintentionally
- Diagnostic endpoints are available at:
  - `/api/diagnostic/azure-function-config`: Shows configuration status
  - `/api/diagnostic/test-azure-function`: Tests the Azure Function integration

#### Troubleshooting Azure Function Integration

If conversations aren't being saved to the database, check the following:

1. **Configuration values**: Ensure these environment variables are set in both local and Azure environments:
   - `AZURE_FUNCTION_URL`: The Azure Function endpoint URL
   - `AZURE_FUNCTION_KEY`: The Function authentication key 
   - `AZURE_FUNCTION_USER_ID`: (Optional) User ID to associate with conversations
   - `AZURE_FUNCTION_USER_EMAIL`: (Optional) User email to associate with conversations

2. **Deployment issues**: Check application logs for `AZURE FUNCTION DEBUG` and `AZURE FUNCTION CONFIG` messages
   - These enhanced logs show if configuration values are missing in the Azure environment
   - They also provide details about any exceptions occurring during the conversation save process

3. **Function behavior**: Remember that the Azure Function has two paths:
   - When no conversationId is provided, it creates a new record
   - When conversationId is provided, it attempts to update an existing record
   - We intentionally omit conversationId to always create new records

## Document Processing Configuration

### Token Management

The application is configured to handle large documents with the following token management settings:

- **Maximum Token Estimate**: 12,000 tokens (increased from 7,000)
- **Tokens Per Character Ratio**: 4 characters ≈ 1 token (estimated)
- **High-Priority Content**: Automatically prioritizes content with specific page references and content matching user queries
- **Chunk Distribution Algorithm**: Uses a balanced distribution algorithm to select chunks across the entire document when all chunks can't fit within token limits

### Large Document Handling

1. **Document Chunking**: Documents are divided into semantic chunks to maintain context
2. **Entity Recognition**: Important entities are extracted and indexed for quick retrieval
3. **Page Reference Handling**: Special handling for specific page references in user queries
4. **Balanced Content Selection**: When all content can't fit within token limits, the system selects a balanced distribution of chunks

## Next Steps

1. ✅ Complete document search improvements
2. ✅ Test with large documents and complex queries
3. Optimize Azure Function integration
4. Prepare for deployment to staging environment
5. Conduct user acceptance testing

## PowerShell Command Syntax Notes

### Important Command Differences

1. **Command Chaining**: 
   - Unix/Bash uses: `command1 && command2`
   - PowerShell uses: `command1; command2` or `command1; if ($?) { command2 }`

---

# Project Snapshot - July 2025

## Recent Fixes and Improvements

### Dependency Injection Fixes

1. **Interface Implementation**
   - Fixed `EnhanceChunksWithMetadata` method in `DocumentChunkingService` - changed from `private` to `public` to properly implement `IDocumentChunkingService`
   - Correctly implemented all service interfaces to support proper dependency injection
   - Ensured ChatService injects `IDocumentChunkingService` interface instead of the concrete class

2. **Service Registration**
   - All services are properly registered with their interfaces in `Program.cs`
   - Constructor injection consistently uses interfaces rather than concrete implementations
   - Fixed DI chain to prevent `System.InvalidOperationException` during service resolution

3. **Interface Hierarchy**
   - Implemented missing interfaces:
     - `IDocumentSearchService`
     - `IDocumentChunkingService`  
     - `ISemanticChunker`
     - `IOpenAIService`
     - `IAzureFunctionService`

### Architecture Clean-up

1. **File Organization**
   - Removed duplicate `ChatController.cs.new` file to prevent build conflicts
   - Ensured all interfaces are in the `Backend.Services.Interfaces` namespace
   - Maintained consistent implementation of interfaces across service classes

2. **Front-end Improvements**
   - Added cache busting parameters to JavaScript modules to prevent stale code issues
   - Fixed import/export path issues in modular JavaScript
   - Ensured proper document status indicator functionality

## Troubleshooting Common Issues

### 500 Internal Server Errors

- Check dependency injection configuration in `Program.cs`
- Verify all concrete implementations properly implement their interfaces
- Confirm all service constructor parameters use interfaces instead of concrete types
- Review Azure App Service logs for detailed error messages

### Document Processing Failures

- Verify `EnhanceChunksWithMetadata` and other critical methods are public
- Check if file size exceeds limits in the Azure App Service
- Review document chunking parameters for very large documents

### Azure OpenAI Integration

- Ensure these environment variables are correctly set in Azure App Service:
  - `OPENAI_API_KEY`
  - `OPENAI_ENDPOINT`
  - `OPENAI_DEPLOYMENT_NAME` 
  - `OPENAI_API_VERSION`

### Session Handling

- The system uses both server-side session IDs and client-provided session IDs for document context
- Client session IDs are stored in cookies and persisted between page reloads
- Document context is tied to these session identifiers

## Key API Endpoints

- **Basic Chat**: `/api/chat` (POST)
- **Document Upload & Chat**: `/api/document-chat/with-file` (POST)
- **Clear Document Context**: `/api/document-chat/clear-context` (POST)
- **Configuration**: `/api/config` (GET)
- **OpenAI Connection Test**: `/api/config/test-openai` (GET)
- **System Diagnostic Info**: `/api/config/diagnostic` (GET)

## Deployment Notes

Build errors will occur if:
1. Interface methods are implemented with incorrect visibility (private/internal vs. public)
2. Service injections use concrete classes instead of interfaces
3. Required services are not properly registered in `Program.cs`

Runtime errors (500) will occur if:
1. DI container cannot resolve the complete chain of dependencies
2. Environment variables for Azure OpenAI are missing or incorrect
3. File processing exceeds memory or timeout limits

*Note: Always commit interface changes and dependency injection fixes together to prevent build and runtime errors during deployment.*

2. **Directory Navigation**: 
   - Use `cd` or `Set-Location` with separate commands
   - Example: `cd Backend; dotnet build` instead of `cd Backend && dotnet build`

3. **Error Handling**:
   - PowerShell uses `$?` or `$LASTEXITCODE` to check previous command success
   - Use `if ($LASTEXITCODE -eq 0) { ... }` for conditional execution

## Document Upload Debugging

We're investigating 400 Bad Request errors when uploading documents to both the new endpoint `/api/document-chat/with-file` and the legacy fallback endpoint `/api/chat/with-file`. Enhanced logging has been added to help diagnose these issues.

# Azure Function Conversation Persistence - July 2025

## Implementation Status

The application now successfully persists chat conversations to the database via an Azure Function. Each chat interaction creates a new conversation record in the database, with proper error handling and logging throughout the process.

## Configuration Requirements

### Environment Variables

The following environment variables must be correctly configured in the Azure App Service:

- `AZURE_FUNCTION_URL`: The complete URL to the Azure Function endpoint (e.g., `https://fn-conversationsave.azurewebsites.net/api/conversations/update`)
- `AZURE_FUNCTION_KEY`: The function key required for authentication
- `AZURE_FUNCTION_USER_ID`: (Optional) User ID to associate with conversations
- `AZURE_FUNCTION_USER_EMAIL`: (Optional) User email to associate with conversations

### Alternative Configuration

These values can also be specified in `appsettings.json` using the following structure:

```json
{
  "AzureFunction": {
    "Url": "https://fn-conversationsave.azurewebsites.net/api/conversations/update",
    "Key": "your-function-key",
    "UserId": "optional-user-id",
    "UserEmail": "optional-user-email"
  }
}
```

> **Note:** Environment variables take precedence over `appsettings.json` values.

## Implementation Details

### Service Architecture

1. **IAzureFunctionService Interface**: Defines the contract for saving conversations
2. **AzureFunctionService Implementation**: Handles the actual HTTP calls to the Azure Function
3. **ChatService Integration**: Calls the Azure Function service after successful chat completions

### Key Implementation Features

- **Fire-and-forget Pattern**: Conversations are saved asynchronously without blocking the response
- **Enhanced Logging**: Detailed logging of environment detection, configuration, and HTTP requests/responses
- **Robust Error Handling**: Multi-level try-catch blocks with inner exception details
- **Configuration Fallbacks**: Multiple sources for configuration with proper precedence
- **Automatic User Info**: Auto-generates user ID and email if not provided
- **New Record Creation**: Intentionally omits conversationId to always create new records
- **Transaction Tracking**: Unique IDs for each save operation for correlation in logs

## Diagnostic Tools

### Diagnostic Endpoints

1. **Verify Azure Function Configuration**:
   ```
   GET /api/diagnostic/verify-azure-function-service
   ```
   Shows current configuration status, environment detection, and values

2. **Direct Azure Function Testing**:
   ```
   GET /api/diagnostic/direct-azure-function-test
   ```
   Performs a complete test of the Azure Function integration with detailed step-by-step results

### Log Messages to Monitor

- `AZURE_FUNCTION_SAVE_START [id]`: Indicates a save operation has begun
- `AZURE_FUNCTION_SAVE_SUCCESS [id]`: Indicates successful completion
- `AZURE_FUNCTION_SAVE_ERROR [id]`: Shows detailed error information
- `AZURE_FUNCTION_SAVE_TIMEOUT [id]`: Indicates a timeout occurred (after 30 seconds)
- `CONFIGURATION DEBUG`: Detailed configuration detection and resolution information

## Troubleshooting Guide

### Common Issues

1. **Configuration Missing**
   - **Symptom**: `Azure Function URL not configured` in logs
   - **Solution**: Verify environment variables in Azure App Service Configuration

2. **Permission Issues**
   - **Symptom**: 401/403 responses from Azure Function
   - **Solution**: Verify function key is correct and function app allows the calls

3. **Network Issues**
   - **Symptom**: Timeout errors or connection failures
   - **Solution**: Check network security groups, firewall rules, and VNET settings

4. **Environment Detection**
   - **Symptom**: Configuration values available in one service but not another
   - **Solution**: Use `/api/diagnostic/verify-azure-function-service` to check environment detection

5. **Record Creation Issues**
   - **Symptom**: Records being updated instead of created as new
   - **Solution**: Verify the code is not sending a conversationId in the payload

### Manual Testing

1. Send a test chat message
2. Check application logs for `AZURE_FUNCTION_SAVE_SUCCESS`
3. Verify database records show the new conversation
4. If issues persist, use the direct test endpoint to isolate the problem
