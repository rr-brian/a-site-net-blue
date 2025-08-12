using System.Threading.Tasks;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for OpenAI service operations
    /// </summary>
    public interface IOpenAIService
    {
        /// <summary>
        /// Gets a chat completion from the Azure OpenAI API
        /// </summary>
        /// <param name="message">The user message to process</param>
        /// <param name="conversationId">Optional conversation ID for context</param>
        /// <returns>The generated response text</returns>
        Task<string> GetChatCompletionAsync(string message, string? conversationId);
    }
}
