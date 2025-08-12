using System.ComponentModel.DataAnnotations;

namespace Backend.Configuration;

/// <summary>
/// Configuration class for Entra ID (Azure AD) authentication settings
/// </summary>
public class EntraIdConfiguration
{
    public const string SectionName = "EntraId";

    /// <summary>
    /// Azure AD tenant ID
    /// </summary>
    [Required]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Application (client) ID registered in Azure AD
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD instance (usually https://login.microsoftonline.com/)
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Domain name for the Azure AD tenant
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Audience for JWT token validation
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Scopes required for API access
    /// </summary>
    public List<string> Scopes { get; set; } = new() { "openid", "profile", "email" };

    /// <summary>
    /// Validates if the configuration is properly set up
    /// </summary>
    public bool IsConfigured => 
        !string.IsNullOrWhiteSpace(TenantId) && 
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(Instance);

    /// <summary>
    /// Gets the authority URL for token validation
    /// </summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}";

    /// <summary>
    /// Gets the issuer URL for JWT validation
    /// </summary>
    public string Issuer => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
