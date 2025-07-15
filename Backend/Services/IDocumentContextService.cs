using System.Threading.Tasks;
using Backend.Models;
using System.Collections.Generic;

namespace Backend.Services
{
    /// <summary>
    /// Interface for document context preparation and management
    /// </summary>
    public interface IDocumentContextService
    {
        /// <summary>
        /// Prepares document context for LLM consumption with token management
        /// </summary>
        string PrepareDocumentContext(DocumentInfo documentInfo, string userMessage);
        
        /// <summary>
        /// Process a document, create chunks and prepare document info with metadata
        /// </summary>
        Task<DocumentInfo> ProcessDocumentAsync(string documentText, string fileName, List<string>? searchTerms = null, List<int>? pageReferences = null);
    }
}
