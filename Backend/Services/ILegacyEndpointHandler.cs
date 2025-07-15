using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Backend.Services
{
    /// <summary>
    /// Interface for legacy endpoint handling to maintain backward compatibility
    /// </summary>
    public interface ILegacyEndpointHandler
    {
        /// <summary>
        /// Process a legacy document upload and chat request
        /// </summary>
        Task<IActionResult> HandleLegacyChatWithFileRequest(HttpRequest request, string? sessionId = null, string? clientSessionId = null);
    }
}
