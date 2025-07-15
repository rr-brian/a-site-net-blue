using System;
using System.Collections.Generic;

namespace Backend.Services
{
    /// <summary>
    /// Interface for analyzing user chat messages and extracting relevant information
    /// </summary>
    public interface IChatAnalysisService
    {
        /// <summary>
        /// Extract search terms from a user message to identify relevant document chunks
        /// </summary>
        List<string> ExtractSearchTerms(string message);

        /// <summary>
        /// Extract requested page numbers from a user message
        /// </summary>
        List<int> ExtractPageReferences(string message);
    }
}
