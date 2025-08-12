using Backend.Models;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for semantic document chunking operations
    /// </summary>
    public interface ISemanticChunker
    {
        /// <summary>
        /// Process a document with advanced semantic chunking inspired by LangChain
        /// </summary>
        /// <param name="content">Document text content</param>
        /// <param name="fileName">Name of the document file</param>
        /// <returns>DocumentInfo containing processed chunks and metadata</returns>
        DocumentInfo ProcessDocument(string content, string fileName);
    }
}
