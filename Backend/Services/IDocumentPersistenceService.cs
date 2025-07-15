using Backend.Models;

namespace Backend.Services
{
    /// <summary>
    /// Interface for services that handle document persistence between requests
    /// </summary>
    public interface IDocumentPersistenceService
    {
        /// <summary>
        /// Stores a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="document">Document to store</param>
        void StoreDocument(string sessionId, DocumentInfo document);
        
        /// <summary>
        /// Retrieves a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Document if found, null otherwise</returns>
        DocumentInfo GetDocument(string sessionId);
        
        /// <summary>
        /// Removes a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        void ClearDocument(string sessionId);
    }
}
