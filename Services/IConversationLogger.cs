namespace RaiToolbox.Services;

public interface IConversationLogger
{
    Task<bool> LogConversationAsync(ConversationLog log);
}

public class ConversationLog
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ChatType { get; set; } = "web";
    public List<LogMessage> Messages { get; set; } = new();
    public int TotalTokens { get; set; }
    public ConversationMetadata Metadata { get; set; } = new();
}

public class LogMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ConversationMetadata
{
    public string Source { get; set; } = "web";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
}