namespace RaiToolbox.Services;

public interface IAuthenticationService
{
    Task<string> GetAccessTokenAsync();
    Task<UserInfo> GetUserInfoAsync(string accessToken);
    bool IsAuthenticated(HttpContext context);
    string GetUserEmail(HttpContext context);
    string GetUserId(HttpContext context);
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
}