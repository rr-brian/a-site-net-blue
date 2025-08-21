using System.Text;
using Newtonsoft.Json;

namespace RaiToolbox.Services;

public class ConversationLogger : IConversationLogger
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConversationLogger> _logger;
    private readonly string _functionUrl;
    private readonly string _functionKey;

    public ConversationLogger(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ConversationLogger> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _functionUrl = _configuration["AzureFunction:Url"] ?? string.Empty;
        _functionKey = _configuration["AzureFunction:Key"] ?? string.Empty;
    }

    public async Task<bool> LogConversationAsync(ConversationLog log)
    {
        try
        {
            if (string.IsNullOrEmpty(_functionUrl))
            {
                _logger.LogWarning("Azure Function URL not configured, skipping conversation logging");
                return false;
            }

            var httpClient = _httpClientFactory.CreateClient();
            
            if (!string.IsNullOrEmpty(_functionKey))
            {
                httpClient.DefaultRequestHeaders.Add("x-functions-key", _functionKey);
            }

            var json = JsonConvert.SerializeObject(log);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_functionUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Conversation logged successfully for user {log.UserEmail}");
                return true;
            }
            else
            {
                _logger.LogWarning($"Failed to log conversation. Status: {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging conversation to Azure Function");
            return false;
        }
    }
}