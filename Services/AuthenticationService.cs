using System.Security.Claims;
using Microsoft.Identity.Web;

namespace RaiToolbox.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IConfiguration configuration,
        ILogger<AuthenticationService> logger,
        ITokenAcquisition tokenAcquisition)
    {
        _configuration = configuration;
        _tokenAcquisition = tokenAcquisition ?? throw new ArgumentNullException(nameof(tokenAcquisition));
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var scopes = new[] { "user.read" };
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
            return accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring access token");
            throw;
        }
    }

    public async Task<UserInfo> GetUserInfoAsync(string accessToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var userInfo = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(json);
                return userInfo ?? new UserInfo();
            }

            return new UserInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user info from Graph API");
            return new UserInfo();
        }
    }

    public bool IsAuthenticated(HttpContext context)
    {
        return context.User?.Identity?.IsAuthenticated ?? false;
    }

    public string GetUserEmail(HttpContext context)
    {
        if (!IsAuthenticated(context))
            return string.Empty;

        var email = context.User?.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            email = context.User?.FindFirst("preferred_username")?.Value;
        }
        if (string.IsNullOrEmpty(email))
        {
            email = context.User?.FindFirst(ClaimTypes.Name)?.Value;
        }

        return email ?? string.Empty;
    }

    public string GetUserId(HttpContext context)
    {
        if (!IsAuthenticated(context))
            return string.Empty;

        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            userId = context.User?.FindFirst("oid")?.Value;
        }
        if (string.IsNullOrEmpty(userId))
        {
            userId = context.User?.FindFirst("sub")?.Value;
        }

        return userId ?? string.Empty;
    }
}