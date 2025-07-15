# AI Chat Application System Architecture

## Overview

This document provides a comprehensive overview of the architecture and configuration of the AI Chat application, focusing on the recent refactoring work that improved separation of concerns, maintainability, and reliability.

## Controller Architecture

The application has been refactored from a monolithic controller structure into a more maintainable architecture with three specialized controllers:

### 1. ChatController (`ChatController.cs`)
- **Responsibility**: Core chat functionality without document context
- **Key Endpoint**: `/api/chat` (POST)
- **Functionality**: 
  - Processes basic chat requests
  - Checks for document context if requested by client
  - Maintains session tracking for reliable document context

### 2. DocumentChatController (`DocumentChatController.cs`)
- **Responsibility**: Document upload, processing, and context management
- **Key Endpoints**:
  - `/api/document-chat/with-file` (POST) - Upload document and process chat
  - `/api/document-chat/clear-context` (POST) - Clear document context
- **Functionality**:
  - Document text extraction
  - Semantic chunking
  - Entity detection (e.g., "ITA Group" mentions)
  - Document persistence across sessions

### 3. ConfigController (`ConfigController.cs`)
- **Responsibility**: Configuration and diagnostic endpoints
- **Key Endpoints**:
  - `/api/config` (GET) - Frontend configuration settings
  - `/api/config/test-openai` (GET) - Test OpenAI connectivity
  - `/api/config/diagnostic` (GET) - Detailed system configuration
- **Functionality**:
  - Provides configuration values to the frontend
  - Diagnostics for API connectivity
  - Environment information

## Service Architecture

The application uses the following services:

### 1. ChatService
- Processes chat requests
- Integrates with Azure OpenAI API
- Applies retry logic for API rate limits
- Incorporates document context when available

### 2. DocumentProcessingService
- Extracts text from various file formats (PDF, DOCX, XLSX)
- Processes document content for the AI

### 3. DocumentChunkingService & SemanticChunker
- Divides documents into meaningful chunks
- Creates metadata for each chunk
- Detects important entities and pages

### 4. DocumentPersistenceService
- Stores document information between sessions
- Uses both server session ID and client session ID for reliability
- Implements robust JSON serialization/deserialization

### 5. DocumentContextService
- Prepares document context for inclusion in chat prompts
- Prioritizes relevant chunks based on user messages
- Special handling for important sections (e.g., page 42, ITA Group mentions)

### 6. PromptEngineeringService
- Creates effective system prompts
- Incorporates document context when available
- Applies special instructions for document-based responses

## Session Management

The application uses a dual approach to session management for improved reliability:

1. **Server Session**
   - ASP.NET Core session management
   - HTTP-only cookies with SameSite=Lax
   - Primarily used for server-side operations

2. **Client Session**
   - Persistent client session ID stored in localStorage
   - Generated on first visit and maintained across browser sessions
   - Used as a fallback when server sessions change

## Document Context Flow

1. **Document Upload**
   - Document uploaded via `/api/document-chat/with-file`
   - Text extracted and processed into chunks
   - Document stored with both server session ID and client session ID

2. **Chat with Document Context**
   - Client sends request with `maintainDocumentContext: true`
   - Backend retrieves document using server session ID
   - Falls back to client session ID if server session has changed
   - Document chunks included in prompt

3. **Document Context Clearing**
   - Client requests context clearing via `/api/document-chat/clear-context`
   - Backend clears document for both server and client session IDs
   - Client updates localStorage to reflect no active document

## Frontend Architecture

### API Integration (`api.js`)
- `callChatAPI` - Basic chat requests
- `callChatWithFileAPI` - Document upload and chat
- `clearDocumentContext` - Clear document context
- Uses fetch API with credentials: 'same-origin'
- Maintains document context state in localStorage

### Document Handling (`document-handler.js`)
- Manages document upload UI
- Handles document context clearing
- Updates UI document state indicator

### UI Components
- Chat message display
- Document context indicator
- File upload functionality
- Error handling and user feedback

## Configuration Settings

### File Upload Settings
- Max file size (default: 10MB)
- Supported extensions: .pdf, .docx, .xlsx

### Chat Settings
- Max messages shown (default: 50)

### OpenAI API Configuration
- Azure OpenAI endpoint and API key
- Deployment name configuration
- Fallback to standard OpenAI if Azure not available

## Security Considerations

- Document content remains server-side
- Only metadata exposed to client
- Session cookies are HTTP-only with SameSite=Lax
- API keys never exposed to frontend

## Environment Configuration

Configuration is managed through:
- appsettings.json
- appsettings.Development.json
- Environment variables
- Azure app configuration (when deployed)

## Next Steps and Future Improvements

1. Further modularize large services (DocumentPersistenceService, DocumentContextService)
2. Add automated tests for document persistence and session handling
3. Improve error recovery and logging for rare edge cases
4. Consider frontend UI modularization for improved separation of concerns
5. Add memory/performance optimization for large documents

---

*Last Updated: July 14, 2025*
