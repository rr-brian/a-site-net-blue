using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Configuration;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    /// <summary>
    /// Service for constructing effective prompts for LLM interactions
    /// </summary>
    public class PromptEngineeringService : Interfaces.IPromptEngineeringService
    {
        private readonly ILogger<PromptEngineeringService> _logger;
        private readonly OpenAIConfiguration _openAIConfig;
        
        public PromptEngineeringService(OpenAIConfiguration openAIConfig, ILogger<PromptEngineeringService> logger)
        {
            _openAIConfig = openAIConfig;
            _logger = logger;
        }
        
        /// <summary>
        /// Create a system prompt for the AI, with optional document context
        /// </summary>
        public string CreateSystemPrompt(DocumentInfo? documentInfo = null, string? documentContext = null)
        {
            // Start with the base system prompt from configuration
            var systemPromptBuilder = new StringBuilder();
            systemPromptBuilder.AppendLine(_openAIConfig.SystemPrompt ?? string.Empty);
            
            // Add document context instructions if we have document info
            if (documentInfo != null)
            {
                systemPromptBuilder.AppendLine("\nYou have been provided with document context from the document: " + documentInfo.FileName);
                systemPromptBuilder.AppendLine("When answering questions about this document:");
                systemPromptBuilder.AppendLine("1. Provide specific page numbers when citing information using the format [Page X].");
                systemPromptBuilder.AppendLine("2. If asked about a page or section that isn't included in your context, clearly state that you don't have access to that specific part of the document.");
                systemPromptBuilder.AppendLine("3. If asked about ITA Group, make sure to highlight key information about their space requirements, leasing terms, and any other relevant details.");
                systemPromptBuilder.AppendLine("4. Be precise about square footage numbers, dates, and other numerical data.");
                
                // Add page 42 specific instructions if it exists in document or is requested
                if (documentContext != null && (documentContext.Contains("PAGE 42") || documentContext.Contains("Page 42")))
                {
                    systemPromptBuilder.AppendLine("\nIMPORTANT: Page 42 of this document contains significant information. Pay close attention to content from this page when relevant to the query.");
                }
                
                // Add ITA Group specific instructions if they exist in the document
                if (documentInfo.EntityIndex != null && documentInfo.EntityIndex.ContainsKey("ITA Group"))
                {
                    systemPromptBuilder.AppendLine("\nIMPORTANT: This document contains information about ITA Group. When discussing ITA Group:");
                    systemPromptBuilder.AppendLine("1. Bold the name **ITA Group** in your responses.");
                    systemPromptBuilder.AppendLine("2. Provide specific square footage information when available.");
                    systemPromptBuilder.AppendLine("3. Reference the specific pages where ITA Group information appears.");
                }
                
                // Add a separator before the actual document content
                systemPromptBuilder.AppendLine("\n=== DOCUMENT CONTENT BELOW ===\n");
                
                // Add the document context if provided
                if (!string.IsNullOrEmpty(documentContext))
                {
                    systemPromptBuilder.AppendLine(documentContext);
                }
                else
                {
                    systemPromptBuilder.AppendLine("No document content was provided due to token limits. Please inform the user and suggest they try with a more specific question.");
                }
                
                systemPromptBuilder.AppendLine("\n=== END OF DOCUMENT CONTENT ===\n");
            }
            
            string finalPrompt = systemPromptBuilder.ToString();
            int estimatedTokens = finalPrompt.Length / 4; // Rough estimate: 1 token ≈ 4 chars
            
            _logger.LogInformation("Created system prompt with {Length} characters (~{Tokens} tokens)",
                finalPrompt.Length, estimatedTokens);
                
            return finalPrompt;
        }
        
        /// <summary>
        /// Create user message with additional context or instructions if needed
        /// </summary>
        public string EnhanceUserMessage(string originalMessage)
        {
            // Currently just returns the original message, but could be extended
            // to add instructions or context based on message analysis
            return originalMessage;
        }
    }
}
