using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Service for processing document files and extracting text content
    /// </summary>
    public interface IDocumentProcessingService
    {
        /// <summary>
        /// Extract text content from a document file
        /// </summary>
        /// <param name="file">The document file to process</param>
        /// <returns>Extracted text content from the document</returns>
        Task<string> ExtractTextFromDocument(IFormFile file);
    }
}
