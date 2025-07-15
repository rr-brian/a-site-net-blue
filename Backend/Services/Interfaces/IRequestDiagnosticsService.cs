using Microsoft.AspNetCore.Http;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Service for diagnosing and logging details about HTTP requests
    /// </summary>
    public interface IRequestDiagnosticsService
    {
        /// <summary>
        /// Logs detailed information about an HTTP request
        /// </summary>
        /// <param name="context">The HTTP context to analyze</param>
        void LogRequestDetails(HttpContext context);
        
        /// <summary>
        /// Extracts form data from a request into a dictionary
        /// </summary>
        /// <param name="context">The HTTP context containing form data</param>
        /// <returns>Dictionary of form key-value pairs</returns>
        Task<Dictionary<string, string>> ExtractFormDataAsync(HttpContext context);
        
        /// <summary>
        /// Logs detailed information about an uploaded file
        /// </summary>
        /// <param name="file">The file to analyze</param>
        void LogFileDetails(IFormFile file);
    }
}
