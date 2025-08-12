using System.Security.Claims;
using Backend.Services.Interfaces;

namespace Backend.Services;

/// <summary>
/// Service for handling authentication-related operations with Entra ID JWT tokens
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(ILogger<AuthenticationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the current authenticated user's information from JWT claims
    /// </summary>
    public AuthenticatedUser GetCurrentUser(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Attempted to get current user for unauthenticated request");
            return new AuthenticatedUser();
        }

        var authenticatedUser = new AuthenticatedUser
        {
            Id = GetClaimValue(user, ClaimTypes.NameIdentifier) ?? GetClaimValue(user, "oid") ?? string.Empty,
            Name = GetClaimValue(user, ClaimTypes.Name) ?? GetClaimValue(user, "name") ?? string.Empty,
            Email = GetClaimValue(user, ClaimTypes.Email) ?? GetClaimValue(user, "preferred_username") ?? string.Empty,
            TenantId = GetClaimValue(user, "tid") ?? string.Empty,
            Roles = GetClaimValues(user, ClaimTypes.Role),
            Scopes = GetScopesFromClaims(user)
        };

        _logger.LogDebug("Retrieved user information for {UserId} ({UserName})", 
            authenticatedUser.Id, authenticatedUser.Name);

        return authenticatedUser;
    }

    /// <summary>
    /// Validates if the user has the required permissions for the operation
    /// </summary>
    public bool HasPermission(ClaimsPrincipal user, string requiredScope)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userScopes = GetScopesFromClaims(user);
        var hasPermission = userScopes.Contains(requiredScope, StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug("Permission check for scope '{RequiredScope}': {HasPermission}", 
            requiredScope, hasPermission);

        return hasPermission;
    }

    /// <summary>
    /// Extracts the user's unique identifier from JWT claims
    /// </summary>
    public string GetUserId(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return string.Empty;
        }

        return GetClaimValue(user, ClaimTypes.NameIdentifier) ?? 
               GetClaimValue(user, "oid") ?? 
               string.Empty;
    }

    /// <summary>
    /// Helper method to get a single claim value
    /// </summary>
    private static string? GetClaimValue(ClaimsPrincipal user, string claimType)
    {
        return user.FindFirst(claimType)?.Value;
    }

    /// <summary>
    /// Helper method to get multiple claim values
    /// </summary>
    private static List<string> GetClaimValues(ClaimsPrincipal user, string claimType)
    {
        return user.FindAll(claimType).Select(c => c.Value).ToList();
    }

    /// <summary>
    /// Extracts scopes from JWT claims (handles both 'scp' and 'scope' claim types)
    /// </summary>
    private static List<string> GetScopesFromClaims(ClaimsPrincipal user)
    {
        var scopes = new List<string>();

        // Check for 'scp' claim (single scope string)
        var scpClaim = GetClaimValue(user, "scp");
        if (!string.IsNullOrWhiteSpace(scpClaim))
        {
            scopes.AddRange(scpClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // Check for 'scope' claims (can be multiple)
        var scopeClaims = GetClaimValues(user, "scope");
        foreach (var scopeClaim in scopeClaims)
        {
            if (!string.IsNullOrWhiteSpace(scopeClaim))
            {
                scopes.AddRange(scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
