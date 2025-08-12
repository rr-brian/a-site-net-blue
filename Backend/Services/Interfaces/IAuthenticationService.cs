using System.Security.Claims;

namespace Backend.Services.Interfaces;

/// <summary>
/// Service interface for handling authentication-related operations
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Gets the current authenticated user's information
    /// </summary>
    /// <param name="user">ClaimsPrincipal from the HTTP context</param>
    /// <returns>User information extracted from JWT claims</returns>
    AuthenticatedUser GetCurrentUser(ClaimsPrincipal user);

    /// <summary>
    /// Validates if the user has the required permissions for the operation
    /// </summary>
    /// <param name="user">ClaimsPrincipal from the HTTP context</param>
    /// <param name="requiredScope">Required scope for the operation</param>
    /// <returns>True if user has permission, false otherwise</returns>
    bool HasPermission(ClaimsPrincipal user, string requiredScope);

    /// <summary>
    /// Extracts the user's unique identifier from JWT claims
    /// </summary>
    /// <param name="user">ClaimsPrincipal from the HTTP context</param>
    /// <returns>Unique user identifier</returns>
    string GetUserId(ClaimsPrincipal user);
}

/// <summary>
/// Represents an authenticated user with relevant information
/// </summary>
public class AuthenticatedUser
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public List<string> Scopes { get; set; } = new();
}
