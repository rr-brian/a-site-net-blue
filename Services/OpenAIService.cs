using Azure;
using Azure.AI.OpenAI;

namespace RaiToolbox.Services;

public class OpenAIService : IOpenAIService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIService> _logger;
    private readonly OpenAIClient _client;
    private readonly string _deploymentName;

    public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var endpoint = _configuration["AzureOpenAI:Endpoint"] ?? 
            throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var apiKey = _configuration["AzureOpenAI:ApiKey"] ?? 
            throw new InvalidOperationException("Azure OpenAI API key not configured");
        _deploymentName = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4.1";

        _client = new OpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _logger.LogInformation($"OpenAI service initialized with endpoint: {endpoint}");
    }

    public async Task<ChatResponse> GetChatResponseAsync(ChatRequest request)
    {
        try
        {
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _deploymentName,
                MaxTokens = request.MaxTokens,
                Temperature = (float)request.Temperature
            };

            foreach (var message in request.Messages)
            {
                switch (message.Role.ToLower())
                {
                    case "user":
                        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message.Content));
                        break;
                    case "assistant":
                        chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage(message.Content));
                        break;
                    case "system":
                        chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(message.Content));
                        break;
                    default:
                        chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message.Content));
                        break;
                }
            }

            var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
            
            if (response.Value.Choices.Count > 0)
            {
                var choice = response.Value.Choices[0];
                return new ChatResponse
                {
                    Content = choice.Message.Content,
                    TotalTokens = response.Value.Usage.TotalTokens,
                    Success = true
                };
            }

            return new ChatResponse
            {
                Error = "No response from OpenAI",
                Success = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response from OpenAI");
            return new ChatResponse
            {
                Error = $"Error: {ex.Message}",
                Success = false
            };
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var testRequest = new ChatRequest
            {
                Messages = new List<ChatMessage>
                {
                    new() { Role = "user", Content = "Hello" }
                },
                MaxTokens = 10
            };

            var response = await GetChatResponseAsync(testRequest);
            return response.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return false;
        }
    }
}