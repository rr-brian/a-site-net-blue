using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Models;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for chat processing service
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Process a chat request with optional document context
        /// </summary>
        /// <param name="message">User message</param>
        /// <param name="documentInfo">Optional document context</param>
        /// <returns>Response from chat service</returns>
        Task<string> ProcessChatRequest(string message, DocumentInfo? documentInfo = null);
        
        /// <summary>
        /// Process a chat request with conversation history and optional document context
        /// </summary>
        /// <param name="message">User message</param>
        /// <param name="conversationHistory">Previous conversation messages</param>
        /// <param name="documentInfo">Optional document context</param>
        /// <returns>Response from chat service</returns>
        Task<string> ProcessChatRequestWithHistory(string message, List<ChatHistoryMessage> conversationHistory, DocumentInfo? documentInfo = null);
        
        /// <summary>
        /// Process a chat request with a newly uploaded document
        /// </summary>
        /// <param name="userMessage">User message</param>
        /// <param name="documentText">Document text content</param>
        /// <param name="fileName">Document file name</param>
        /// <returns>Tuple of response text and document info</returns>
        Task<(string Response, DocumentInfo DocumentInfo)> ProcessChatWithDocument(
            string userMessage, 
            string documentText, 
            string fileName);
    }
}
