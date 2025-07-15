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
                // Log detailed information about the incoming request
                _logger.LogInformation("Legacy endpoint received request with: File={FileName}, FileSize={FileSize}, Message={MessageLength}, ClientSessionId={SessionId}",
                    file?.FileName ?? "<no file>", 
                    file?.Length ?? 0,
                    message?.Length ?? 0, 
                    clientSessionId ?? "<not provided>");
                
                // Log form data for debugging
                _logger.LogInformation("Form data keys: {Keys}", string.Join(", ", HttpContext.Request.Form.Keys));
                
                // Log file details if present
                if (file != null)
                {
                    _logger.LogInformation("File details - Name: {Name}, ContentType: {ContentType}, Length: {Length}",
                        file.FileName, file.ContentType, file.Length);
                }
                else
                {
                    _logger.LogWarning("File is null - checking for file in Request.Form.Files");
                    if (HttpContext.Request.Form.Files.Count > 0)
                    {
                        var formFile = HttpContext.Request.Form.Files[0];
                        _logger.LogInformation("Found file in Form.Files: {Name}, ContentType: {ContentType}, Length: {Length}",
                            formFile.FileName, formFile.ContentType, formFile.Length);
                        file = formFile; // Use the file from form files
                    }
                    else
                    {
                        _logger.LogError("No file found in request");
                        return BadRequest("No file uploaded");
                    }
                }

                // Check for missing parameters
                if (file.Length == 0)
                {
                    _logger.LogError("Legacy endpoint file is empty: {FileName}", file.FileName);
                    return BadRequest($"Uploaded file '{file.FileName}' is empty");
                }
                
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogError("Legacy endpoint missing message");
                    // Check if message might be in another form field
                    foreach (var key in HttpContext.Request.Form.Keys)
                    {
                        if (key.ToLower().Contains("message") && !string.IsNullOrEmpty(HttpContext.Request.Form[key]))
                        {
                            message = HttpContext.Request.Form[key];
                            _logger.LogWarning("Found message in alternate form field {Field}: {Message}", key, message);
                            break;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(message))
                    {
                        return BadRequest("No message provided");
                    }
                }
                
                // File validation - more lenient to help with debugging
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                _logger.LogInformation("File extension: {Extension}", extension);
                if (extension != ".pdf" && extension != ".docx" && extension != ".xlsx")
                {
                    _logger.LogWarning("Legacy endpoint received potentially unsupported file format: {Extension}", extension);
                    // Continue anyway for debugging purposes
                }
                
                // Try to access file contents to verify it's readable
                try
                {
                    using var stream = file.OpenReadStream();
                    var buffer = new byte[Math.Min(1024, file.Length)];
                    await stream.ReadAsync(buffer, 0, buffer.Length);
                    stream.Position = 0; // Reset position after test read
                    _logger.LogInformation("Successfully verified file is readable");
                }
                catch (Exception readEx)
                {
                    _logger.LogError(readEx, "Failed to read file contents");
                    return BadRequest($"File cannot be read: {readEx.Message}");
                }

                // Get the DocumentChatController and its services via DI
                var documentChatController = (DocumentChatController)HttpContext.RequestServices
                    .GetService(typeof(DocumentChatController));
                
                if (documentChatController == null)
                {
                    _logger.LogError("Failed to resolve DocumentChatController from DI");
                    return StatusCode(500, new { error = "Server error resolving document controller" });
                }
                
                // Set the controller's ControllerContext to use our current HttpContext
                documentChatController.ControllerContext = new ControllerContext
                {
                    HttpContext = HttpContext
                };
                
                try
                {
                    _logger.LogInformation("Forwarding to DocumentChatController.ChatWithFile with: File={FileName}, Message={MessageLength}, ClientSessionId={SessionId}", 
                        file.FileName, message.Length, clientSessionId ?? "<not provided>");
                    
                    var result = await documentChatController.ChatWithFile(file, message, clientSessionId);
                    
                    // Log the result type
                    _logger.LogInformation("DocumentChatController.ChatWithFile returned result of type: {ResultType}", result.GetType().Name);
                    
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling DocumentChatController.ChatWithFile");
                    return StatusCode(500, new { error = $"Error processing file: {ex.Message}", details = ex.StackTrace });
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
