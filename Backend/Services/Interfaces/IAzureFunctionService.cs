using System.Threading.Tasks;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for Azure Function operations
    /// </summary>
    public interface IAzureFunctionService
    {
        /// <summary>
        /// Saves conversation data to an Azure Function endpoint
        /// </summary>
        /// <param name="userMessage">The user message</param>
        /// <param name="aiResponse">The AI's response</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task SaveConversationAsync(string userMessage, string aiResponse);
    }
}
