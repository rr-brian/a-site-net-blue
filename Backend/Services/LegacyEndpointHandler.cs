using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Backend.Models;
using Backend.Services;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    /// <summary>
    /// Handles legacy endpoints to maintain backward compatibility
    /// </summary>
    public class LegacyEndpointHandler : Interfaces.ILegacyEndpointHandler
    {
        private readonly Microsoft.Extensions.Logging.ILogger<LegacyEndpointHandler> _logger;
        private readonly Backend.Services.Interfaces.IFileValidationService _fileValidationService;
        private readonly Backend.Services.Interfaces.IRequestDiagnosticsService _requestDiagnosticsService;
        private readonly Backend.Services.Interfaces.IChatService _chatService;
        private readonly Backend.Services.Interfaces.IDocumentProcessingService _documentProcessingService;
        private readonly Backend.Services.Interfaces.IDocumentContextService _documentContextService;
        private readonly Backend.Services.Interfaces.IDocumentPersistenceService _documentPersistenceService;
        private readonly Backend.Services.Interfaces.IChatAnalysisService _chatAnalysisService;
        
        public LegacyEndpointHandler(
            Microsoft.Extensions.Logging.ILogger<LegacyEndpointHandler> logger,
            Backend.Services.Interfaces.IFileValidationService fileValidationService,
            Backend.Services.Interfaces.IRequestDiagnosticsService requestDiagnosticsService,
            Backend.Services.Interfaces.IChatService chatService,
            Backend.Services.Interfaces.IDocumentProcessingService documentProcessingService,
            Backend.Services.Interfaces.IDocumentContextService documentContextService,
            Backend.Services.Interfaces.IDocumentPersistenceService documentPersistenceService,
            Backend.Services.Interfaces.IChatAnalysisService chatAnalysisService)
        {
            _logger = logger;
            _fileValidationService = fileValidationService;
            _requestDiagnosticsService = requestDiagnosticsService;
            _chatService = chatService;
            _documentProcessingService = documentProcessingService;
            _documentContextService = documentContextService;
            _documentPersistenceService = documentPersistenceService;
            _chatAnalysisService = chatAnalysisService;
        }
        
        /// <summary>
        /// Process a legacy document upload and chat request
        /// </summary>
        public async Task<IActionResult> HandleLegacyChatWithFileRequest(
            HttpRequest request,
            string? sessionId = null,
            string? clientSessionId = null)
        {
            _logger.LogWarning("Legacy endpoint /api/chat/with-file called. Handling request.");
            
            try
            {
                // Create a new context with access to the request data
                var httpContext = new DefaultHttpContext();
                // We'll use the original request for form data access
                string message = "";
                IFormFile? file = null;
                
                // Log detailed request information
                _logger.LogInformation("Processing legacy request");
                
                // Extract form data directly from the request
                var formData = new Dictionary<string, string>();
                foreach (var key in request.Form.Keys)
                {
                    if (key != "file" && !string.IsNullOrEmpty(request.Form[key]))
                    {
                        formData[key] = request.Form[key];
                        _logger.LogInformation("Form field {Key}: {Value}", key, request.Form[key]);
                    }
                }
                
                // Get session ID from the provided value or create a new one
                sessionId = sessionId ?? Guid.NewGuid().ToString();
                
                // Handle file from form
                if (request.Form.Files.Count > 0)
                {
                    file = request.Form.Files[0];
                    _logger.LogInformation("Using file from form collection: {FileName}", file.FileName);
                }
                else
                {
                    _logger.LogError("No file found in request");
                    return new BadRequestObjectResult("File is required but not found in request");
                }
                
                // Log file details
                _requestDiagnosticsService.LogFileDetails(file);
                
                // Validate file (with less strict validation for legacy support)
                var (isValid, errorMessage) = await _fileValidationService.ValidateFileAsync(file, false);
                if (!isValid)
                {
                    _logger.LogWarning("File validation failed with message: {Message}", errorMessage);
                    // For legacy endpoint, we'll only return error if the file is completely unusable
                    if (file.Length == 0)
                    {
                        return new BadRequestObjectResult(errorMessage);
                    }
                }
                
                // Handle message from form data
                if (formData.TryGetValue("message", out var formMessage) && 
                    !string.IsNullOrWhiteSpace(formMessage))
                {
                    message = formMessage;
                    _logger.LogInformation("Retrieved message from form data: {Length} chars", message.Length);
                }
                else
                {
                    // Check if message might be in another form field
                    foreach (var key in request.Form.Keys)
                    {
                        if (key.ToLower().Contains("message") && !string.IsNullOrEmpty(request.Form[key]))
                        {
                            message = request.Form[key];
                            _logger.LogWarning("Found message in alternate form field {Field}: {Length} chars", 
                                key, message.Length);
                            break;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(message))
                    {
                        return new BadRequestObjectResult("No message provided");
                    }
                }
                
                _logger.LogInformation("Processing document chat request directly with services");
                _logger.LogInformation("Message length: {Length}, ClientSessionId: {ClientSessionId}", 
                    message.Length, clientSessionId ?? "<none>");
                
                try
                {
                    // Extract text from the document
                    string documentText = await _documentProcessingService.ExtractTextFromDocument(file);
                    _logger.LogInformation("Extracted {Length} characters from document", documentText.Length);
                    
                    // Extract search terms and page references from the message
                    var searchTerms = _chatAnalysisService.ExtractSearchTerms(message);
                    var pageReferences = _chatAnalysisService.ExtractPageReferences(message);
                    
                    // Process the document
                    var documentInfo = await _documentContextService.ProcessDocumentAsync(
                        documentText, 
                        file.FileName, 
                        searchTerms, 
                        pageReferences);
                    
                    // Store the document using persistence service with the session ID
                    await _documentPersistenceService.StoreDocumentAsync(sessionId, documentInfo);
                    _logger.LogInformation("Document saved with session ID: {SessionId}", sessionId);
                    
                    // Also store with client session ID if available
                    if (!string.IsNullOrEmpty(clientSessionId))
                    {
                        await _documentPersistenceService.StoreDocumentAsync(clientSessionId, documentInfo);
                        _logger.LogInformation("Document also saved with client session ID: {ClientSessionId}", clientSessionId);
                    }
                    
                    // Get the response from the chat service
                    string responseText;
                    if (documentInfo != null)
                    {
                        responseText = await _chatService.ProcessChatRequest(message, documentInfo);
                    }
                    else
                    {
                        _logger.LogWarning("DocumentInfo is unexpectedly null before chat processing in legacy endpoint handler");
                        responseText = await _chatService.ProcessChatRequest(message);
                    }
                    
                    // Create response object
                    var chatResponse = new ChatResponse
                    {
                        Response = responseText,
                        DocumentInContext = true,
                        DocumentInfo = new {
                            fileName = documentInfo.FileName,
                            chunkCount = documentInfo.Chunks?.Count ?? 0
                        }
                    };
                    
                    return new OkObjectResult(chatResponse);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing document: {Message}\nStack: {StackTrace}", 
                        ex.Message, ex.StackTrace);
                    return new ObjectResult("Internal server error processing document") { StatusCode = 500 };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in legacy endpoint handler: {Message}\nStack: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return new ObjectResult("Internal server error processing legacy request") { StatusCode = 500 };
            }
        }
    }
}
