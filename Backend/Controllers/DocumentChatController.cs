using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;
using Backend.Configuration;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/document-chat")]
    public class DocumentChatController : ControllerBase
    {
        private readonly ILogger<DocumentChatController> _logger;
        private readonly IConfiguration _configuration;
        private readonly Backend.Services.Interfaces.IChatService _chatService;
        private readonly Backend.Services.Interfaces.IDocumentProcessingService _documentProcessingService;
        private readonly Backend.Services.Interfaces.IDocumentChunkingService _documentChunkingService;
        private readonly Backend.Services.Interfaces.ISemanticChunker _semanticChunker;
        private readonly Backend.Services.Interfaces.IDocumentPersistenceService _documentPersistenceService;
        private readonly Backend.Services.Interfaces.IFileValidationService _fileValidationService;
        private readonly Backend.Services.Interfaces.IRequestDiagnosticsService _requestDiagnosticsService;
        private readonly Backend.Services.Interfaces.IDocumentContextService _documentContextService;
        private readonly Backend.Services.Interfaces.IChatAnalysisService _chatAnalysisService;
        private readonly Backend.Services.Interfaces.IPromptEngineeringService _promptEngineeringService;
        
        public DocumentChatController(
            ILogger<DocumentChatController> logger,
            IConfiguration configuration,
            Backend.Services.Interfaces.IChatService chatService,
            Backend.Services.Interfaces.IDocumentProcessingService documentService,
            Backend.Services.Interfaces.IDocumentChunkingService documentChunkingService,
            Backend.Services.Interfaces.ISemanticChunker semanticChunker,
            Backend.Services.Interfaces.IDocumentPersistenceService documentPersistenceService,
            Backend.Services.Interfaces.IFileValidationService fileValidationService,
            Backend.Services.Interfaces.IRequestDiagnosticsService requestDiagnosticsService,
            Backend.Services.Interfaces.IDocumentContextService documentContextService,
            Backend.Services.Interfaces.IChatAnalysisService chatAnalysisService,
            Backend.Services.Interfaces.IPromptEngineeringService promptEngineeringService)
        {
            _logger = logger;
            _configuration = configuration;
            _chatService = chatService;
            _documentProcessingService = documentService;
            _documentChunkingService = documentChunkingService;
            _semanticChunker = semanticChunker;
            _documentPersistenceService = documentPersistenceService;
            _fileValidationService = fileValidationService;
            _requestDiagnosticsService = requestDiagnosticsService;
            _documentContextService = documentContextService;
            _chatAnalysisService = chatAnalysisService;
            _promptEngineeringService = promptEngineeringService;
        }

        [HttpPost("with-file")]
        [RequestSizeLimit(52428800)] // 50MB limit explicitly set for this endpoint
        [RequestFormLimits(MultipartBodyLengthLimit = 52428800)] // 50MB for multipart
        public async Task<IActionResult> ChatWithFile(IFormFile file, [FromForm] string message, [FromForm] string? clientSessionId = null)
        {
            // Log request details
            _requestDiagnosticsService.LogRequestDetails(HttpContext);
            
            try
            {
                _logger.LogInformation("DocumentChatController.ChatWithFile called");
                
                // Log detailed file information
                _requestDiagnosticsService.LogFileDetails(file);
                
                // Validate file
                var (isValid, errorMessage) = await _fileValidationService.ValidateFileAsync(file);
                if (!isValid)
                {
                    _logger.LogError("ChatWithFile: File validation failed: {ErrorMessage}", errorMessage);
                    return BadRequest(errorMessage);
                }
                
                // Validate message
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogError("ChatWithFile: Message parameter is empty or null");
                    return BadRequest("No message provided");
                }
                
                // Extract search terms from the message to better prioritize relevant content
                var searchTerms = _chatAnalysisService.ExtractSearchTerms(message);
                var pageReferences = _chatAnalysisService.ExtractPageReferences(message);
                
                _logger.LogInformation("Extracted {Count} search terms and {PageCount} page references from message", 
                    searchTerms.Count, pageReferences.Count);
                
                // Extract text from the document
                string documentText;
                try
                {
                    _logger.LogInformation("Extracting text from document: {FileName}", file.FileName);
                    documentText = await _documentProcessingService.ExtractTextFromDocument(file);
                    _logger.LogInformation("Successfully extracted {TextLength} characters from document", documentText.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ChatWithFile: Error extracting text from document: {Message}", ex.Message);
                    if (ex.InnerException != null) {
                        _logger.LogError("Inner exception: {Message}", ex.InnerException.Message);
                    }
                    return StatusCode(500, new { error = $"Unable to extract text from document: {ex.Message}" });
                }
                
                _logger.LogInformation("Extracted {TextLength} characters of text from document", documentText.Length);
                
                // Process the document using our DocumentContextService
                DocumentInfo documentInfo;
                try
                {
                    documentInfo = await _documentContextService.ProcessDocumentAsync(documentText, file.FileName, searchTerms, pageReferences);
                    
                    if (documentInfo == null || documentInfo.Chunks == null || documentInfo.Chunks.Count == 0)
                    {
                        _logger.LogError("ChatWithFile: Document processing failed");
                        return BadRequest("Document processing failed");
                    }
                }
                catch (Exception chunkEx)
                {
                    _logger.LogError(chunkEx, "ChatWithFile: Error during document chunking");
                    return StatusCode(500, new { error = $"Error processing document: {chunkEx.Message}" });
                }
                
                _logger.LogInformation("Semantic chunker created {Count} chunks with improved entity detection", documentInfo.Chunks.Count);
                
                // Log if we found ITA Group in the document
                if (documentInfo.EntityIndex.ContainsKey("ITA Group"))
                {
                    var itaChunks = documentInfo.EntityIndex["ITA Group"];
                    _logger.LogInformation("Found ITA Group in {Count} chunks", itaChunks.Count);
                    foreach (var chunkIndex in itaChunks.Take(3)) // Log up to 3 examples
                    {
                        _logger.LogInformation("ITA Group mention in chunk {Index}: {Preview}", 
                            chunkIndex, 
                            documentInfo.Chunks[chunkIndex].Length > 50 
                                ? documentInfo.Chunks[chunkIndex].Substring(0, 50) + "..." 
                                : documentInfo.Chunks[chunkIndex]);
                    }
                }
                
                // Get the server session ID
                string sessionId = HttpContext.Session.Id;
                _logger.LogInformation("Server session ID: {SessionId}", sessionId);
                
                // Handle client-provided session ID if any
                string clientSessionIdToUse = clientSessionId;
                if (string.IsNullOrEmpty(clientSessionIdToUse))
                {
                    // Try to get from session if not provided directly
                    clientSessionIdToUse = HttpContext.Session.GetString("ClientSessionId");
                }
                
                // Store the document using persistence service with both session IDs
                try
                {
                    // Store with server session ID
                    await _documentPersistenceService.StoreDocumentAsync(sessionId, documentInfo);
                    _logger.LogInformation("Document saved with server session ID: {SessionId}", sessionId);
                    
                    // Also store with client session ID if available
                    if (!string.IsNullOrEmpty(clientSessionIdToUse))
                    {
                        await _documentPersistenceService.StoreDocumentAsync(clientSessionIdToUse, documentInfo);
                        _logger.LogInformation("Document also saved with client session ID: {ClientSessionId}", clientSessionIdToUse);
                        
                        // Remember this client session ID for future use
                        HttpContext.Session.SetString("ClientSessionId", clientSessionIdToUse);
                    }
                    else
                    {
                        _logger.LogWarning("No client session ID provided - document only stored with server session ID");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ChatWithFile: Error storing document: {Message}", ex.Message);
                    // Continue despite storage error - we can still return the chat response
                }
                
                _logger.LogInformation("Document saved to persistence service: {FileName} with {ChunkCount} chunks", 
                    documentInfo.FileName, documentInfo.Chunks.Count);
                
                // Process the chat with the document
                string response;
                try
                {
                    // Prepare document context using the DocumentContextService
                    string documentContext = _documentContextService.PrepareDocumentContext(documentInfo, message);
                    _logger.LogInformation("Prepared document context: {Length} characters", documentContext?.Length ?? 0);
                    
                    // Create system prompt using the PromptEngineeringService - this is now done internally by ProcessChatRequest
                    _logger.LogInformation("Using document context for chat request");
                    
                    // Process the chat request with document context
                    // The systemPrompt parameter has been removed as it's now handled internally by the ChatService
                    // Ensure documentInfo is not null (even though it should never be at this point)
                    if (documentInfo != null)
                    {
                        response = await _chatService.ProcessChatRequest(message, documentInfo);
                    }
                    else
                    {
                        _logger.LogWarning("DocumentInfo is unexpectedly null before chat processing");
                        response = await _chatService.ProcessChatRequest(message);
                    }
                }
                catch (Exception chatEx)
                {
                    _logger.LogError(chatEx, "ChatWithFile: Error during chat processing");
                    return StatusCode(500, new { error = $"Error processing chat: {chatEx.Message}" });
                }
                
                return Ok(new { 
                    response = response, 
                    documentStored = true,
                    documentInContext = true,
                    documentInfo = new {
                        fileName = documentInfo.FileName,
                        chunkCount = documentInfo.Chunks.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file {FileName}: {ErrorMessage}", file?.FileName ?? "unknown", ex.Message);
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, new { error = $"Error processing file with message: {ex.Message}" });
            }
        }
        
        [HttpPost("clear-context")]
        public IActionResult ClearDocumentContext()
        {
            try
            {
                _logger.LogInformation("Clearing document context");
                string sessionId = HttpContext.Session.Id;
                _documentPersistenceService.ClearDocument(sessionId);
                
                // Also try to clear with client session ID if it exists
                string clientSessionIdToUse = HttpContext.Session.GetString("ClientSessionId");
                if (!string.IsNullOrEmpty(clientSessionIdToUse))
                {
                    _logger.LogInformation("Also clearing document context for client session ID: {ClientSessionId}", clientSessionIdToUse);
                    _documentPersistenceService.ClearDocument(clientSessionIdToUse);
                }
                
                return Ok(new { success = true, message = "Document context cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing document context");
                return StatusCode(500, new { success = false, error = "Error clearing document context" });
            }
        }
    }
}
