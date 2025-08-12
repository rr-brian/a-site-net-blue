using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Backend.Configuration
{
    public class OpenAIConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIConfiguration> _logger;

        public OpenAIConfiguration(IConfiguration configuration, ILogger<OpenAIConfiguration> logger)
        {
            _configuration = configuration;
            _logger = logger;
            Initialize();
        }

        public string? Endpoint { get; private set; }
        public string? ApiKey { get; private set; }
        public string? DeploymentName { get; private set; }
        public string? SystemPrompt { get; private set; }
        public bool IsConfigured => !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(ApiKey);

        private void Initialize()
        {
            try
            {
                // First try environment variables (highest priority for Azure deployment)
                Endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
                ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                
                // Get deployment name from environment but validate it's a known working deployment
                string envDeploymentName = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
                // IMPORTANT: Only "gpt-4.1" works in this Azure resource based on testing
                if (envDeploymentName != null && envDeploymentName == "gpt-4.1")
                {
                    DeploymentName = envDeploymentName;
                }
                else
                {
                    // Override environment variable with known working deployment
                    DeploymentName = "gpt-4.1";
                    if (!string.IsNullOrEmpty(envDeploymentName))
                    {
                        _logger.LogWarning("Environment variable OPENAI_DEPLOYMENT_NAME has value '{Value}' which is not supported. Using 'gpt-4.1' instead.", envDeploymentName);
                    }
                }
                // Try to get API version, though not critical for basic functionality
                var apiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");
                if (!string.IsNullOrEmpty(apiVersion))
                {
                    _logger.LogInformation("Found OPENAI_API_VERSION: {ApiVersion}", apiVersion);
                }
                // System prompt can come from config or fallback to default
                
                // Add detailed debug logging
                _logger.LogWarning("DEBUG - OpenAI Configuration - Environment Variables:");
                _logger.LogWarning("OPENAI_ENDPOINT value: {Endpoint}", Endpoint ?? "<null>");
                _logger.LogWarning("OPENAI_API_KEY exists: {HasKey}", !string.IsNullOrEmpty(ApiKey));
                _logger.LogWarning("OPENAI_DEPLOYMENT_NAME value: {DeploymentName}", DeploymentName ?? "<null>");
                _logger.LogWarning("OPENAI_API_VERSION exists: {HasApiVersion}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_VERSION")));
                
                _logger.LogInformation("Checking environment variables: OPENAI_ENDPOINT={HasEndpoint}, OPENAI_API_KEY={HasApiKey}", 
                    !string.IsNullOrEmpty(Endpoint),
                    !string.IsNullOrEmpty(ApiKey));
                
                // First, try the OpenAI section (prioritize this configuration)
                if (string.IsNullOrEmpty(Endpoint))
                    Endpoint = _configuration["OpenAI:Endpoint"];
                
                if (string.IsNullOrEmpty(ApiKey))
                    ApiKey = _configuration["OpenAI:ApiKey"];
                
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = _configuration["OpenAI:DeploymentName"];
                
                if (string.IsNullOrEmpty(SystemPrompt))
                    SystemPrompt = _configuration["OpenAI:SystemPrompt"];

                // If OpenAI section isn't available, try AzureOpenAI section as fallback
                if (string.IsNullOrEmpty(Endpoint))
                    Endpoint = _configuration["AzureOpenAI:Endpoint"];
                
                if (string.IsNullOrEmpty(ApiKey))
                    ApiKey = _configuration["AzureOpenAI:ApiKey"];
                
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = _configuration["AzureOpenAI:DeploymentName"];
                
                if (string.IsNullOrEmpty(SystemPrompt))
                    SystemPrompt = _configuration["AzureOpenAI:SystemPrompt"];

                // Log configuration status
                _logger.LogInformation("OpenAI Configuration: Endpoint={HasEndpoint}, ApiKey={HasApiKey}, DeploymentName={DeploymentName}",
                    !string.IsNullOrEmpty(Endpoint), 
                    !string.IsNullOrEmpty(ApiKey), 
                    DeploymentName);
                
                // Set defaults if still null
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = "gpt-4.1";  // This is the only deployment that exists in the Azure resource
                
                if (string.IsNullOrEmpty(SystemPrompt))
                    SystemPrompt = "You are a helpful AI assistant.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing OpenAI configuration");
            }
        }
    }
}
