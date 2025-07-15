using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services;
using System.IO;

namespace Backend.Controllers
{
    /// <summary>
    /// Handles core chat functionality without document context
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly ChatService _chatService;
        private readonly IDocumentPersistenceService _documentPersistenceService;
        
        public ChatController(
            ILogger<ChatController> logger,
            ChatService chatService,
            IDocumentPersistenceService documentPersistenceService)
        {
            _logger = logger;
            _chatService = chatService;
            _documentPersistenceService = documentPersistenceService;
        }

        /// <summary>
        /// Processes a chat request, with optional document context
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ChatResponse>> PostAsync([FromBody] ChatRequest request)
        {
            try
            {
                _logger.LogInformation("Received chat request. MaintainDocumentContext: {MaintainContext}", request.MaintainDocumentContext);
                
                // Get the document from our persistence service using the session ID
                string sessionId = HttpContext.Session.Id;
                _logger.LogInformation("Processing chat request for session ID: {SessionId}", sessionId);
                
                // Use client-provided session ID as a fallback if available
                string clientSessionId = request.ClientSessionId;
                if (!string.IsNullOrEmpty(clientSessionId))
                {
                    _logger.LogInformation("Client provided session ID: {ClientSessionId}", clientSessionId);
                    
                    // Store the association between server session and client session
                    HttpContext.Session.SetString("ClientSessionId", clientSessionId);
                }
                
                // Store session ID in a cookie so we can verify if it's changing between requests
                HttpContext.Response.Cookies.Append("LastSessionId", sessionId, new CookieOptions { HttpOnly = false, IsEssential = true });
                
                // Check if there's a cookie with previous session ID to see if it changed
                if (HttpContext.Request.Cookies.TryGetValue("LastSessionId", out var lastSessionId))
                {
                    if (lastSessionId != sessionId)
                    {
                        _logger.LogError("SESSION CHANGED! Previous session: {PreviousSession}, Current session: {CurrentSession}", 
                            lastSessionId, sessionId);
                    }
                    else
                    {
                        _logger.LogInformation("Session ID consistent with previous request: {SessionId}", sessionId);
                    }
                }
                
                // Ensure session is active and persisted
                if (!HttpContext.Session.IsAvailable)
                {
                    _logger.LogWarning("Session is not available. Creating a new session.");
                    HttpContext.Session.SetString("SessionCheck", "Active"); // Force session creation
                    sessionId = HttpContext.Session.Id;
                    _logger.LogInformation("Created new session with ID: {SessionId}", sessionId);
                }
                
                DocumentInfo documentInfo = null;
                
                // If the client wants to maintain document context, get it from our persistence service
                if (request.MaintainDocumentContext)
                {
                    _logger.LogInformation("Maintaining document context as requested for session: {SessionId}", sessionId);
                    
                    // First try with server session ID
                    documentInfo = _documentPersistenceService.GetDocument(sessionId);
                    
                    // If not found but we have a client session ID, try with that as fallback
                    if (documentInfo == null && !string.IsNullOrEmpty(request.ClientSessionId))
                    {
                        _logger.LogWarning("Document not found with server session ID, trying client session ID: {ClientSessionId}", request.ClientSessionId);
                        documentInfo = _documentPersistenceService.GetDocument(request.ClientSessionId);
                        
                        // If found with client ID, re-save with server session ID for future requests
                        if (documentInfo != null)
                        {
                            _logger.LogInformation("Found document using client session ID, re-saving with server session ID");
                            _documentPersistenceService.StoreDocument(sessionId, documentInfo);
                        }
                    }
                    
                    if (documentInfo != null)
                    {
                        _logger.LogInformation("SUCCESS: Retrieved document from persistence service: {FileName} with {ChunkCount} chunks for session {SessionId}",
                            documentInfo.FileName, documentInfo.Chunks?.Count ?? 0, sessionId);
                        
                        // Also save with client session ID if available for redundancy
                        if (!string.IsNullOrEmpty(request.ClientSessionId) && sessionId != request.ClientSessionId)
                        {
                            _logger.LogInformation("Redundantly storing document with client session ID: {ClientSessionId}", request.ClientSessionId);
                            _documentPersistenceService.StoreDocument(request.ClientSessionId, documentInfo);
                        }
                        
                        // Add detailed logging about the document chunks
                        if (documentInfo.Chunks == null || documentInfo.Chunks.Count == 0)
                        {
                            _logger.LogWarning("Document exists but has NO CHUNKS. This is likely an error. Session: {SessionId}", sessionId);
                        }
                        else
                        {
                            // Log some sample chunk content for debugging
                            _logger.LogInformation("First chunk sample for session {SessionId}: {Sample}", 
                                sessionId,
                                documentInfo.Chunks[0].Length > 50 ? documentInfo.Chunks[0].Substring(0, 50) + "..." : documentInfo.Chunks[0]);
                                
                            // Log number of chunks with metadata
                            _logger.LogInformation("Document has {ChunkCount} chunks and {MetadataCount} metadata entries",
                                documentInfo.Chunks.Count,
                                documentInfo.ChunkMetadata?.Count ?? 0);
                                
                            // Specifically look for page 42 in chunks
                            bool foundPage42 = false;
                            foreach (var chunk in documentInfo.Chunks)
                            {
                                if (chunk.Contains("PAGE 42 OF") || chunk.Contains("DOCUMENT PAGE 42 of"))
                                {
                                    foundPage42 = true;
                                    _logger.LogInformation("Found PAGE 42 in document chunks: {Preview}", 
                                        chunk.Length > 100 ? chunk.Substring(0, 100) + "..." : chunk);
                                    break;
                                }
                            }
                            
                            if (!foundPage42)
                            {
                                _logger.LogWarning("Could not find PAGE 42 in any document chunks");
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No document found in persistence service for session {SessionId}", sessionId);
                    }
                }
                else
                {
                    // If we're not maintaining document context, clear it from persistence
                    _logger.LogInformation("Not maintaining document context, clearing any existing context");
                    _documentPersistenceService.ClearDocument(sessionId);
                }
                
                // Process the chat request with the document context if available
                string response = await _chatService.ProcessChatRequest(request.Message, documentInfo);
                
                // Return document context info in the response so client knows if a document is being used
                // ALWAYS include document info in response if we have it or if there was a document in context
                var chatResponse = new ChatResponse { 
                    Response = response,
                    DocumentInContext = documentInfo != null || request.MaintainDocumentContext,
                };
                
                // Explicitly include document info when available
                if (documentInfo != null)
                {
                    chatResponse.DocumentInfo = new {
                        FileName = documentInfo.FileName,
                        ChunkCount = documentInfo.Chunks?.Count ?? 0
                    };
                    _logger.LogInformation("Including document info in response: {FileName}", documentInfo.FileName);
                }
                
                return Ok(chatResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                return StatusCode(500, new ChatResponse { Response = "An error occurred while processing your request." });
            }
        }

        /// <summary>
        /// Legacy fallback endpoint for backwards compatibility with old frontend code
        /// Redirects requests to the DocumentChatController
        /// </summary>
        [HttpPost("with-file")]
        public async Task<IActionResult> ChatWithFileFallback(IFormFile file, [FromForm] string message, [FromForm] string clientSessionId)
        {
            _logger.LogWarning("Legacy endpoint /api/chat/with-file called. Redirecting to /api/document-chat/with-file");
            
            try
            {
                // Forward the request to the DocumentChatController (we don't have direct access to it)
                // So we'll reconstruct the request manually
                
                // Create a new memory stream to copy the file
                using var memoryStream = new MemoryStream();
                if (file != null)
                {
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                }

                // Create a new HttpContext with the path modified to the new endpoint
                var originalPath = HttpContext.Request.Path;
                var originalMethod = HttpContext.Request.Method;
                
                _logger.LogInformation("Forwarding {File} with message from legacy endpoint to document-chat controller", 
                    file?.FileName ?? "<no file>");

                // Create a document chat controller and call it directly
                // We're using constructor injection to get services, so we need to get them from the controller's DI
                var documentChatController = (DocumentChatController)HttpContext.RequestServices
                    .GetService(typeof(DocumentChatController));
                
                // Set the controller's ControllerContext to use our current HttpContext
                documentChatController.ControllerContext = new ControllerContext
                {
                    HttpContext = HttpContext
                };
                
                // Call the actual endpoint method
                if (file != null)
                {
                    // Reset the stream position
                    file.OpenReadStream().Position = 0;
                    _logger.LogInformation("Forwarding to DocumentChatController with clientSessionId: {ClientSessionId}", clientSessionId);
                    return await documentChatController.ChatWithFile(file, message, clientSessionId);
                }
                else
                {
                    return BadRequest("No file provided");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in legacy endpoint redirect");
                return StatusCode(500, new { response = "An error occurred while processing your file upload." });
            }
        }
    }
}
