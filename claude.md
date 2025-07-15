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

## Next Steps

1. Complete document search improvements
2. Test with large documents and complex queries
3. Optimize Azure Function integration
4. Prepare for deployment to staging environment
5. Conduct user acceptance testing

## PowerShell Command Syntax Notes

### Important Command Differences

1. **Command Chaining**: 
   - Unix/Bash uses: `command1 && command2`
   - PowerShell uses: `command1; command2` or `command1; if ($?) { command2 }`

2. **Directory Navigation**: 
   - Use `cd` or `Set-Location` with separate commands
   - Example: `cd Backend; dotnet build` instead of `cd Backend && dotnet build`

3. **Error Handling**:
   - PowerShell uses `$?` or `$LASTEXITCODE` to check previous command success
   - Use `if ($LASTEXITCODE -eq 0) { ... }` for conditional execution

## Document Upload Debugging

We're investigating 400 Bad Request errors when uploading documents to both the new endpoint `/api/document-chat/with-file` and the legacy fallback endpoint `/api/chat/with-file`. Enhanced logging has been added to help diagnose these issues.
