namespace RaiToolbox.Services;

public interface IOpenAIService
{
    Task<ChatResponse> GetChatResponseAsync(ChatRequest request);
    Task<bool> TestConnectionAsync();
}

public class ChatRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public int MaxTokens { get; set; } = 1000;
    public double Temperature { get; set; } = 0.7;
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public int TotalTokens { get; set; }
    public string? Error { get; set; }
    public bool Success { get; set; }
}