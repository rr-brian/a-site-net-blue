using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaiToolbox.Services;
using System.Text;

namespace RaiToolbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IOpenAIService _openAIService;
    private readonly IConversationLogger _conversationLogger;
    private readonly IAuthenticationService _authService;
    private readonly IDocumentService _documentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IOpenAIService openAIService,
        IConversationLogger conversationLogger,
        IAuthenticationService authService,
        IDocumentService documentService,
        ILogger<ChatController> logger)
    {
        _openAIService = openAIService;
        _conversationLogger = conversationLogger;
        _authService = authService;
        _documentService = documentService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var userEmail = _authService.GetUserEmail(HttpContext);
            var userId = _authService.GetUserId(HttpContext);

            request.UserId = userId;

            // Get user documents and add context to the request
            _logger.LogInformation($"About to call AddDocumentContextToRequest for user: {userId}");
            await AddDocumentContextToRequest(request, userId);
            _logger.LogInformation("AddDocumentContextToRequest completed successfully");

            var response = await _openAIService.GetChatResponseAsync(request);

            if (response.Success)
            {
                // Log the conversation
                var log = new ConversationLog
                {
                    UserId = userId,
                    UserEmail = userEmail,
                    TotalTokens = response.TotalTokens,
                    Messages = request.Messages.Select(m => new LogMessage
                    {
                        Role = m.Role,
                        Content = m.Content
                    }).ToList()
                };

                log.Messages.Add(new LogMessage
                {
                    Role = "assistant",
                    Content = response.Content
                });

                await _conversationLogger.LogConversationAsync(log);

                return Ok(response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }

    [HttpGet("test")]
    public async Task<IActionResult> TestConnection()
    {
        var result = await _openAIService.TestConnectionAsync();
        return Ok(new { connected = result });
    }

    private async Task AddDocumentContextToRequest(ChatRequest request, string userId)
    {
        try
        {
            _logger.LogInformation($"Adding document context for user: {userId}");
            var documents = await _documentService.GetUserDocumentsAsync(userId);
            _logger.LogInformation($"Found {documents.Count} documents for user {userId}");
            
            if (documents.Count > 0)
            {
                var documentContext = new StringBuilder();
                documentContext.AppendLine("AVAILABLE DOCUMENTS AND CONTENT:");
                documentContext.AppendLine("=====================================");
                
                foreach (var doc in documents)
                {
                    documentContext.AppendLine($"\nDocument: {doc.FileName} ({doc.FileType})");
                    documentContext.AppendLine($"Uploaded: {doc.UploadedAt:yyyy-MM-dd}");
                    documentContext.AppendLine("Content:");
                    documentContext.AppendLine("---");
                    
                    // Get the actual text content from the document
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", $"{doc.Id}_{doc.FileName}");
                    if (System.IO.File.Exists(filePath))
                    {
                        var content = await _documentService.ExtractTextFromDocumentAsync(filePath);
                        documentContext.AppendLine(content);
                    }
                    else
                    {
                        documentContext.AppendLine($"[File content for {doc.FileName} not available]");
                    }
                    documentContext.AppendLine("---\n");
                }

                // Add system message with document context at the beginning
                var systemMessage = new ChatMessage
                {
                    Role = "system",
                    Content = $"You have access to the following documents uploaded by the user. Use this information to answer their questions:\n\n{documentContext}",
                    Timestamp = DateTime.UtcNow
                };

                request.Messages.Insert(0, systemMessage);
                
                _logger.LogInformation($"Added context for {documents.Count} documents to chat request");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document context to chat request");
            // Don't fail the request if we can't load document context
        }
    }
}