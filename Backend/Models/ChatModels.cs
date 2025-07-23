using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Backend.Models
{
    public class ChatRequest
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
        
        [JsonPropertyName("maintainDocumentContext")]
        public bool MaintainDocumentContext { get; set; }
        
        [JsonPropertyName("clientSessionId")]
        public string ClientSessionId { get; set; } = "";
        
        [JsonPropertyName("conversationHistory")]
        public List<ChatHistoryMessage> ConversationHistory { get; set; } = new List<ChatHistoryMessage>();
    }

    public class ChatResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";
        
        [JsonPropertyName("documentInContext")]
        public bool DocumentInContext { get; set; }
        
        [JsonPropertyName("documentInfo")]
        public object DocumentInfo { get; set; }
    }
    
    // DocumentInfo class is already defined in DocumentInfo.cs
    
    public class ChatHistoryRequest
    {
        [JsonPropertyName("messages")]
        public List<ChatHistoryMessage> Messages { get; set; } = new List<ChatHistoryMessage>();
    }
    
    public class ChatHistoryMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";
        
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
    
    public class ChatWithFileRequest
    {
        public string Message { get; set; } = "";
        public bool MaintainDocumentContext { get; set; }
    }
    
    public class ConfigurationResponse
    {
        [JsonPropertyName("configurations")]
        public Dictionary<string, string> ConfigurationValues { get; set; } = new Dictionary<string, string>();
    }
}
