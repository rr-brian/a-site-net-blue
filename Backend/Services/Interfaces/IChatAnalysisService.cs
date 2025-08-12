using System.Collections.Generic;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Service for analyzing user chat messages and extracting relevant information
    /// </summary>
    public interface IChatAnalysisService
    {
        /// <summary>
        /// Extract search terms from a user message to identify relevant document chunks
        /// </summary>
        /// <param name="message">The user message to analyze</param>
        /// <returns>List of extracted search terms</returns>
        List<string> ExtractSearchTerms(string message);

        /// <summary>
        /// Extract requested page numbers from a user message
        /// </summary>
        /// <param name="message">The user message to analyze</param>
        /// <returns>List of page numbers mentioned in the message</returns>
        List<int> ExtractPageReferences(string message);
    }
}
