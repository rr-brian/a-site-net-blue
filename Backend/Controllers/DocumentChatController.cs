using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/document-chat")]
    public class DocumentChatController : ControllerBase
    {
        private readonly ILogger<DocumentChatController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ChatService _chatService;
        private readonly DocumentProcessingService _documentProcessingService;
        private readonly DocumentChunkingService _documentChunkingService;
        private readonly SemanticChunker _semanticChunker;
        private readonly IDocumentPersistenceService _documentPersistenceService;
        
        public DocumentChatController(
            ILogger<DocumentChatController> logger,
            IConfiguration configuration,
            ChatService chatService,
            DocumentProcessingService documentService,
            DocumentChunkingService documentChunkingService,
            SemanticChunker semanticChunker,
            IDocumentPersistenceService documentPersistenceService)
        {
            _logger = logger;
            _configuration = configuration;
            _chatService = chatService;
            _documentProcessingService = documentService;
            _documentChunkingService = documentChunkingService;
            _semanticChunker = semanticChunker;
            _documentPersistenceService = documentPersistenceService;
        }

        [HttpPost("with-file")]
        public async Task<IActionResult> ChatWithFile(IFormFile file, [FromForm] string message, [FromForm] string clientSessionId = null)
        {
            _logger.LogInformation("DocumentChatController.ChatWithFile called with: File={FileName}, FileSize={FileSize}, Message={MessageLength}, ClientSessionId={SessionId}",
                file?.FileName ?? "<no file>", 
                file?.Length ?? 0,
                message?.Length ?? 0, 
                clientSessionId ?? "<not provided>");
            
            try
            {
                // Validate all required parameters
                if (file == null)
                {
                    _logger.LogError("ChatWithFile: File parameter is null");
                    return BadRequest("No file provided");
                }
                
                if (file.Length == 0)
                {
                    _logger.LogError("ChatWithFile: File is empty. FileName: {FileName}", file.FileName);
                    return BadRequest("File is empty");
                }
                
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogError("ChatWithFile: Message parameter is empty or null");
                    return BadRequest("No message provided");
                }
                
                // Log file details for debugging
                _logger.LogInformation("ChatWithFile processing file: Name={FileName}, ContentType={ContentType}, Size={Size}", 
                    file.FileName,
                    file.ContentType,
                    file.Length);
                
                // Check file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension != ".pdf" && extension != ".docx" && extension != ".xlsx")
                {
                    _logger.LogError("ChatWithFile: Unsupported file format: {Extension}", extension);
                    return BadRequest($"Unsupported file format: {extension}. Please upload a PDF, Word, or Excel file.");
                }
                
                // Verify file stream is available
                try 
                {
                    using var stream = file.OpenReadStream();
                    _logger.LogDebug("File stream opened successfully");
                } 
                catch (Exception streamEx) 
                {
                    _logger.LogError(streamEx, "ChatWithFile: Error opening file stream");
                    return StatusCode(500, new { error = $"Unable to read file: {streamEx.Message}" });
                }
                
                // Extract text from the document
                string documentText;
                try
                {
                    documentText = await _documentProcessingService.ExtractTextFromDocument(file);
                    
                    if (string.IsNullOrEmpty(documentText))
                    {
                        _logger.LogError("ChatWithFile: Document text extraction failed - empty result");
                        return BadRequest("Could not extract text from the document");
                    }
                }
                catch (Exception extractEx)
                {
                    _logger.LogError(extractEx, "ChatWithFile: Error extracting text from document");
                    return StatusCode(500, new { error = $"Error extracting text: {extractEx.Message}" });
                }
                
                _logger.LogInformation("Extracted {Length} characters from document {FileName}", documentText.Length, file.FileName);
                
                // Use our semantic chunker for improved document processing
                DocumentInfo documentInfo;
                try
                {
                    documentInfo = _semanticChunker.ProcessDocument(documentText, file.FileName);
                    
                    if (documentInfo == null || documentInfo.Chunks == null || documentInfo.Chunks.Count == 0)
                    {
                        _logger.LogError("ChatWithFile: Document chunking failed");
                        return BadRequest("Document chunking failed");
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
                
                // Process the chat with the document
                string response;
                try
                {
                    response = await _chatService.ProcessChatRequest(message, documentInfo);
                }
                catch (Exception chatEx)
                {
                    _logger.LogError(chatEx, "ChatWithFile: Error during chat processing");
                    return StatusCode(500, new { error = $"Error processing chat: {chatEx.Message}" });
                }
                
                // Store document in our persistence service for future queries
                string sessionId = HttpContext.Session.Id;
                
                // Check if client provided a session ID from method parameter first
                string clientSessionIdToUse = clientSessionId;
                
                // If not provided as parameter, check form data as fallback
                if (string.IsNullOrEmpty(clientSessionIdToUse) && HttpContext.Request.Form.ContainsKey("clientSessionId"))
                {
                    clientSessionIdToUse = HttpContext.Request.Form["clientSessionId"];
                    _logger.LogInformation("Client session ID from form data: {ClientSessionId}", clientSessionIdToUse);
                }
                
                if (!string.IsNullOrEmpty(clientSessionIdToUse))
                {
                    _logger.LogInformation("Using client session ID: {ClientSessionId}", clientSessionIdToUse);
                }
            
                // Store using server session ID
                try
                {
                    _documentPersistenceService.StoreDocument(sessionId, documentInfo);
                    _logger.LogInformation("Document saved using server session ID: {SessionId}", sessionId);
                    
                    // Also store using client session ID if available
                    if (!string.IsNullOrEmpty(clientSessionIdToUse))
                    {
                        _documentPersistenceService.StoreDocument(clientSessionIdToUse, documentInfo);
                        _logger.LogInformation("Document also saved using client session ID: {ClientSessionId}", clientSessionIdToUse);
                        
                        // Store client session ID in session for future reference
                        HttpContext.Session.SetString("ClientSessionId", clientSessionIdToUse);
                    }
                    else
                    {
                        _logger.LogWarning("No client session ID provided with file upload - document only stored with server session ID");
                    }
                }
                catch (Exception storeEx)
                {
                    _logger.LogError(storeEx, "ChatWithFile: Error storing document in persistence service");
                    // Continue despite storage error - we can still return the chat response
                }
                
                _logger.LogInformation("Document saved to persistence service: {FileName} with {ChunkCount} chunks", 
                    documentInfo.FileName, documentInfo.Chunks.Count);
                
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
