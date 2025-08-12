using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Backend.Services;
using System.Threading.Tasks;
using Backend.Models;
using Backend.Configuration;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/config")]
    [Authorize]
    public class ConfigController : ControllerBase
    {
        private readonly ILogger<ConfigController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ChatService _chatService;
        
        public ConfigController(
            ILogger<ConfigController> logger,
            IConfiguration configuration,
            ChatService chatService)
        {
            _logger = logger;
            _configuration = configuration;
            _chatService = chatService;
        }

        [HttpGet]
        public IActionResult GetConfiguration()
        {
            try
            {
                // Get configuration values to expose to the frontend
                var configValues = new
                {
                    MaxFileSize = _configuration["FileUpload:MaxSize"] ?? "10485760",
                    MaxMessagesShown = _configuration["Chat:MaxMessagesShown"] ?? "50",
                    SupportedExtensions = _configuration["FileUpload:SupportedExtensions"] ?? ".pdf,.docx,.xlsx"
                };
                
                return Ok(new
                {
                    ConfigurationValues = configValues
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration");
                return StatusCode(500, new { error = "Error retrieving configuration" });
            }
        }
        
        [HttpGet("test-openai")]
        public async Task<IActionResult> TestOpenAI()
        {
            try
            {
                // Test if we can successfully call the OpenAI API
                string response = await _chatService.ProcessChatRequest("Hello, can you respond with a simple test message?");
                return Ok(new { response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI API test failed");
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}", ex.InnerException.Message);
                }
                return StatusCode(500, new { error = $"OpenAI API test failed: {ex.Message}" });
            }
        }
        
        [HttpGet("diagnostic")]
        public IActionResult ConfigDiagnostic()
        {
            try
            {
                var azureOpenAIEndpoint = _configuration["AzureOpenAI:Endpoint"];
                var openAIEndpoint = _configuration["OpenAI:Endpoint"];
                
                // Safely get API key prefixes
                string azureKey = _configuration["AzureOpenAI:ApiKey"];
                string openAIKey = _configuration["OpenAI:ApiKey"];
                
                var azureOpenAIKey = !string.IsNullOrEmpty(azureKey) ? (azureKey.Substring(0, Math.Min(5, azureKey.Length)) + "...") : "<null>";
                var openAIKeyPrefix = !string.IsNullOrEmpty(openAIKey) ? (openAIKey.Substring(0, Math.Min(5, openAIKey.Length)) + "...") : "<null>";
                var azureDeploymentName = _configuration["AzureOpenAI:DeploymentName"];
                var openAIDeploymentName = _configuration["OpenAI:DeploymentName"];
                
                var diagnosticInfo = new
                {
                    AzureOpenAISection = new
                    {
                        HasEndpoint = !string.IsNullOrEmpty(azureOpenAIEndpoint),
                        EndpointValue = azureOpenAIEndpoint ?? "<null>",
                        HasApiKey = !string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]),
                        ApiKeyPrefix = azureOpenAIKey,
                        DeploymentName = azureDeploymentName
                    },
                    OpenAISection = new
                    {
                        HasEndpoint = !string.IsNullOrEmpty(openAIEndpoint),
                        EndpointValue = openAIEndpoint ?? "<null>",
                        HasApiKey = !string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"]),
                        ApiKeyPrefix = openAIKeyPrefix,
                        DeploymentName = openAIDeploymentName
                    },
                    Environment = new
                    {
                        AspNetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "<null>",
                        HasAzureOpenAIEndpoint = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
                        HasAzureOpenAIApiKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"))
                    },
                    EntraId = new
                    {
                        HasTenantId = !string.IsNullOrEmpty(_configuration["EntraId:TenantId"]),
                        HasClientId = !string.IsNullOrEmpty(_configuration["EntraId:ClientId"]),
                        HasAudience = !string.IsNullOrEmpty(_configuration["EntraId:Audience"]),
                        HasScopes = !string.IsNullOrEmpty(_configuration["EntraId:Scopes"])
                    }
                };
                
                return Ok(diagnosticInfo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Configuration diagnostic failed: {ex.Message}" });
            }
        }
        
        /// <summary>
        /// Public endpoint to get auth configuration for the frontend
        /// This endpoint is not protected to allow the SPA to get configuration before login
        /// </summary>
        [HttpGet("auth")]
        [AllowAnonymous]
        public IActionResult GetAuthConfig()
        {
            try
            {
                // Get Entra ID configuration from app settings
                var tenantId = _configuration["EntraId:TenantId"];
                var clientId = _configuration["EntraId:ClientId"];
                var audience = _configuration["EntraId:Audience"];
                var scopes = _configuration["EntraId:Scopes"];
                
                // Check if configuration is available
                if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
                {
                    _logger.LogWarning("Entra ID configuration missing. TenantId or ClientId not found.");
                    return NotFound(new { error = "Authentication configuration not found" });
                }
                
                // Create auth config for the frontend
                var authConfig = new AuthConfig
                {
                    ClientId = clientId,
                    Authority = $"https://login.microsoftonline.com/{tenantId}",
                    ApiScope = scopes ?? $"api://{audience}/access_as_user"
                };
                
                return Ok(authConfig);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving auth configuration");
                return StatusCode(500, new { error = "Error retrieving auth configuration" });
            }
        }
    }
}
