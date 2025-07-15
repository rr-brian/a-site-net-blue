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
        public async Task<IActionResult> ChatWithFile(IFormFile file, [FromForm] string message)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }
            
            if (string.IsNullOrEmpty(message))
            {
                return BadRequest("No message provided");
            }
            
            _logger.LogInformation("Received chat with file request. File: {FileName}, Message: {Message}", file.FileName, message);
            
            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".docx" && extension != ".xlsx")
            {
                return BadRequest("Unsupported file format. Please upload a PDF, Word, or Excel file.");
            }
            
            try
            {
                // Extract text from the document
                string documentText = await _documentProcessingService.ExtractTextFromDocument(file);
                
                if (string.IsNullOrEmpty(documentText))
                {
                    return BadRequest("Could not extract text from the document");
                }
                
                _logger.LogInformation("Extracted {Length} characters from document {FileName}", documentText.Length, file.FileName);
                
                // Use our new semantic chunker for improved document processing
                var documentInfo = _semanticChunker.ProcessDocument(documentText, file.FileName);
                
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
                string response = await _chatService.ProcessChatRequest(message, documentInfo);
                
                // Store document in our persistence service for future queries
                string sessionId = HttpContext.Session.Id;
                
                // Check if client provided a session ID from cookie/storage
                string clientSessionId = null;
                if (HttpContext.Request.Form.ContainsKey("clientSessionId"))
                {
                    clientSessionId = HttpContext.Request.Form["clientSessionId"];
                    _logger.LogInformation("Client session ID provided with upload: {ClientSessionId}", clientSessionId);
                }
                
                // Store using server session ID
                _documentPersistenceService.StoreDocument(sessionId, documentInfo);
                _logger.LogInformation("Document saved using server session ID: {SessionId}", sessionId);
                
                // Also store using client session ID if available
                if (!string.IsNullOrEmpty(clientSessionId))
                {
                    _documentPersistenceService.StoreDocument(clientSessionId, documentInfo);
                    _logger.LogInformation("Document also saved using client session ID: {ClientSessionId}", clientSessionId);
                    
                    // Store client session ID in session for future reference
                    HttpContext.Session.SetString("ClientSessionId", clientSessionId);
                }
                else
                {
                    _logger.LogWarning("No client session ID provided with file upload - document only stored with server session ID");
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
                _logger.LogError(ex, "Error processing file {FileName}: {ErrorMessage}", file.FileName, ex.Message);
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
                string clientSessionId = HttpContext.Session.GetString("ClientSessionId");
                if (!string.IsNullOrEmpty(clientSessionId))
                {
                    _logger.LogInformation("Also clearing document context for client session ID: {ClientSessionId}", clientSessionId);
                    _documentPersistenceService.ClearDocument(clientSessionId);
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
