using Backend.Models;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for services that construct effective prompts for LLM interactions
    /// </summary>
    public interface IPromptEngineeringService
    {
        /// <summary>
        /// Create a system prompt for the AI, with optional document context
        /// </summary>
        string CreateSystemPrompt(DocumentInfo? documentInfo = null, string? documentContext = null);
        
        /// <summary>
        /// Create user message with additional context or instructions if needed
        /// </summary>
        string EnhanceUserMessage(string originalMessage);
    }
}
