using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Backend.Services.Interfaces
{
    /// <summary>
    /// Interface for handling legacy endpoints to maintain backward compatibility
    /// </summary>
    public interface ILegacyEndpointHandler
    {
        /// <summary>
        /// Process a legacy document upload and chat request
        /// </summary>
        /// <param name="request">The HTTP request</param>
        /// <param name="sessionId">Optional session ID</param>
        /// <param name="clientSessionId">Optional client session ID</param>
        /// <returns>ActionResult with chat response</returns>
        Task<IActionResult> HandleLegacyChatWithFileRequest(
            HttpRequest request,
            string? sessionId = null,
            string? clientSessionId = null);
    }
}
