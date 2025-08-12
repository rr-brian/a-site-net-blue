using System;

namespace Backend.Models
{
    /// <summary>
    /// Model for frontend authentication configuration
    /// </summary>
    public class AuthConfig
    {
        /// <summary>
        /// Client ID for the SPA application
        /// </summary>
        public string ClientId { get; set; }
        
        /// <summary>
        /// Authority URL (tenant endpoint)
        /// </summary>
        public string Authority { get; set; }
        
        /// <summary>
        /// API scope for access tokens
        /// </summary>
        public string ApiScope { get; set; }
    }
}
