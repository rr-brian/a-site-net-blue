using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaiToolbox.Services;

namespace RaiToolbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IAuthenticationService _authService;
    private readonly IOpenAIService _openAIService;

    public ConfigController(
        IConfiguration configuration,
        IAuthenticationService authService,
        IOpenAIService openAIService)
    {
        _configuration = configuration;
        _authService = authService;
        _openAIService = openAIService;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        var config = new
        {
            user = new
            {
                email = _authService.GetUserEmail(HttpContext),
                id = _authService.GetUserId(HttpContext),
                authenticated = _authService.IsAuthenticated(HttpContext)
            },
            app = new
            {
                name = "RAI - RTS AI Toolbox",
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            }
        };

        return Ok(config);
    }

    [HttpGet("diagnostic")]
    public async Task<IActionResult> GetDiagnostic()
    {
        var openAIConnected = await _openAIService.TestConnectionAsync();
        
        var diagnostic = new
        {
            timestamp = DateTime.UtcNow,
            services = new
            {
                openAI = new
                {
                    connected = openAIConnected,
                    endpoint = _configuration["AzureOpenAI:Endpoint"],
                    deployment = _configuration["AzureOpenAI:DeploymentName"]
                },
                authentication = new
                {
                    tenantId = _configuration["EntraId:TenantId"],
                    clientId = _configuration["EntraId:ClientId"],
                    domain = _configuration["EntraId:Domain"]
                },
                azureFunction = new
                {
                    url = _configuration["AzureFunction:Url"],
                    configured = !string.IsNullOrEmpty(_configuration["AzureFunction:Url"])
                }
            },
            user = new
            {
                email = _authService.GetUserEmail(HttpContext),
                authenticated = _authService.IsAuthenticated(HttpContext)
            }
        };

        return Ok(diagnostic);
    }
}