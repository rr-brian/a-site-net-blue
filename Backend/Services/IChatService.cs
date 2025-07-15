using System.Threading.Tasks;
using Backend.Models;

namespace Backend.Services
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
    }
}
