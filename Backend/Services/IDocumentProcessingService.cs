using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Backend.Services
{
    /// <summary>
    /// Interface for document text extraction service
    /// </summary>
    public interface IDocumentProcessingService
    {
        /// <summary>
        /// Extracts text from a document file
        /// </summary>
        /// <param name="file">The document file to extract text from</param>
        /// <returns>Extracted text content</returns>
        Task<string> ExtractTextFromDocument(IFormFile file);
    }
}
