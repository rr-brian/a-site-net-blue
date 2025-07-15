using System.Collections.Generic;
using Backend.Models;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for document search operations
    /// </summary>
    public interface IDocumentSearchService
    {
        /// <summary>
        /// Finds the most relevant chunks for a given query
        /// </summary>
        /// <param name="documentInfo">The document information containing chunks to search</param>
        /// <param name="query">The user query to search for</param>
        /// <returns>List of relevant document chunks</returns>
        List<string> FindRelevantChunks(DocumentInfo documentInfo, string query);
    }
}
