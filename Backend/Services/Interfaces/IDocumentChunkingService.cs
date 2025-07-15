using System.Collections.Generic;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for document chunking operations
    /// </summary>
    public interface IDocumentChunkingService
    {
        /// <summary>
        /// Splits a document into chunks of approximately the specified size
        /// </summary>
        /// <param name="text">The document text to chunk</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <returns>List of document chunks</returns>
        List<string> ChunkDocument(string text, int maxChunkSize = 500);
        
        /// <summary>
        /// Enhances chunks with additional metadata to improve searchability
        /// </summary>
        /// <param name="chunks">The original document chunks</param>
        /// <returns>List of enhanced chunks with metadata</returns>
        List<string> EnhanceChunksWithMetadata(List<string> chunks);
    }
}
