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
                Endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
                ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
                DeploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
                SystemPrompt = Environment.GetEnvironmentVariable("AZURE_OPENAI_SYSTEM_PROMPT");
                
                _logger.LogInformation("Checking environment variables: AZURE_OPENAI_ENDPOINT={HasEndpoint}, AZURE_OPENAI_API_KEY={HasApiKey}", 
                    !string.IsNullOrEmpty(Endpoint),
                    !string.IsNullOrEmpty(ApiKey));
                
                // If environment variables aren't available, try AzureOpenAI section (configuration)
                if (string.IsNullOrEmpty(Endpoint))
                    Endpoint = _configuration["AzureOpenAI:Endpoint"];
                
                if (string.IsNullOrEmpty(ApiKey))
                    ApiKey = _configuration["AzureOpenAI:ApiKey"];
                
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = _configuration["AzureOpenAI:DeploymentName"];
                
                if (string.IsNullOrEmpty(SystemPrompt))
                    SystemPrompt = _configuration["AzureOpenAI:SystemPrompt"];

                // Finally, try the OpenAI section (development environment)
                if (string.IsNullOrEmpty(Endpoint))
                    Endpoint = _configuration["OpenAI:Endpoint"];
                
                if (string.IsNullOrEmpty(ApiKey))
                    ApiKey = _configuration["OpenAI:ApiKey"];
                
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = _configuration["OpenAI:DeploymentName"];
                
                if (string.IsNullOrEmpty(SystemPrompt))
                    SystemPrompt = _configuration["OpenAI:SystemPrompt"];

                // Log configuration status
                _logger.LogInformation("OpenAI Configuration: Endpoint={HasEndpoint}, ApiKey={HasApiKey}, DeploymentName={DeploymentName}",
                    !string.IsNullOrEmpty(Endpoint), 
                    !string.IsNullOrEmpty(ApiKey), 
                    DeploymentName);
                
                // Set defaults for missing values
                if (string.IsNullOrEmpty(DeploymentName))
                    DeploymentName = "gpt-4.1";
                    
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
