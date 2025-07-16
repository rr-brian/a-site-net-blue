using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    public class AzureFunctionService : IAzureFunctionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureFunctionService> _logger;
        private readonly HttpClient _httpClient;

        public AzureFunctionService(
            IConfiguration configuration, 
            ILogger<AzureFunctionService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task SaveConversationAsync(string userMessage, string aiResponse)
        {
            try
            {
                // NOTE: We're intentionally NOT generating a conversation ID here
                // The Azure Function will generate one for us when creating a new record
                
                // Get Azure Function configuration
                var functionUrl = _configuration["AzureFunction:Url"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
                
                var functionKey = _configuration["AzureFunction:Key"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                    
                // Skip if configuration is missing
                if (string.IsNullOrEmpty(functionUrl))
                {
                    _logger.LogWarning("Azure Function URL not configured. Skipping conversation save.");
                    return;
                }
                
                if (string.IsNullOrEmpty(functionKey))
                {
                    _logger.LogWarning("Azure Function key not configured. Skipping conversation save.");
                    return;
                }
                
                // Validate inputs to prevent sending empty messages
                if (string.IsNullOrWhiteSpace(userMessage) || string.IsNullOrWhiteSpace(aiResponse))
                {
                    _logger.LogWarning("Empty message or response provided. Skipping conversation save.");
                    return;
                }

                // Format the conversation data according to the Azure Function's expected schema
                var messages = new[]
                {
                    new { role = "user", content = userMessage },
                    new { role = "assistant", content = aiResponse }
                };

                // Create the conversation payload with additional metadata expected by the Azure Function
                // Get user info from configuration or generate unique values
                var userId = _configuration["AzureFunction:UserId"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID");
                    
                var userEmail = _configuration["AzureFunction:UserEmail"] ?? 
                    Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL");
                
                // If not configured, generate a unique user ID with a timestamp
                if (string.IsNullOrEmpty(userId))
                {
                    userId = $"user-{DateTime.UtcNow.Ticks}";
                }
                
                // If not configured, generate a unique email with a timestamp
                if (string.IsNullOrEmpty(userEmail))
                {
                    userEmail = $"user-{DateTime.UtcNow.Ticks}@realtyts.com";
                }
                
                // Create the conversation payload - IMPORTANT: Omit conversationId to always create new records
                // The Azure Function will generate a new UUID and return it
                var conversation = new
                {
                    // conversationId is intentionally omitted to trigger the "create new" flow in the Azure Function
                    userId,
                    userEmail,
                    chatType = "web",
                    messages = messages,
                    totalTokens = 0,
                    metadata = new
                    {
                        source = "web",
                        timestamp = DateTime.UtcNow.ToString("O")
                    }
                };

                // Build the URL with the function key
                var requestUri = functionUrl;
                if (!string.IsNullOrEmpty(functionKey) && !requestUri.Contains("code="))
                {
                    requestUri = requestUri + (requestUri.Contains("?") ? "&" : "?") + "code=" + functionKey;
                }
                // Log the constructed URL with the key redacted for security
                _logger.LogInformation("Sending conversation to Azure Function at: {FunctionUrl}",
                    functionUrl.Replace(functionKey, "[REDACTED]"));
                    
                // Log request payload for debugging (only in Development environment)
                var requestJson = JsonSerializer.Serialize(conversation);
                _logger.LogInformation("Request payload: {RequestJson}", requestJson);
                
                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                
                // Log what we're sending for diagnostic purposes
                _logger.LogInformation("Sending conversation with UserId: {UserId}, UserEmail: {UserEmail}",
                    userId, userEmail);
                
                // Send the request with detailed error handling
                try
                {
                    var response = await _httpClient.SendAsync(request);
                    
                    // Log response status with more context
                    _logger.LogInformation("Azure Function response status: {StatusCode}", response.StatusCode);
                    
                    // Always read the response content regardless of status code
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        // Try to parse the response to get the conversation ID
                        string newConversationId = "unknown";
                        try {
                            var responseJson = JsonDocument.Parse(responseContent);
                            if (responseJson.RootElement.TryGetProperty("conversationId", out var idElement)) {
                                newConversationId = idElement.GetString() ?? "unknown";
                            }
                        } catch {}
                        
                        _logger.LogInformation("Azure Function call succeeded. Status: {StatusCode}, New ConversationId: {ConversationId}, Response: {Response}", 
                            response.StatusCode, 
                            newConversationId,
                            responseContent);
                            
                        // Log success details including headers
                        foreach (var header in response.Headers)
                        {
                            _logger.LogDebug("Response header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                        }
                    }
                    else
                    {
                        // Log detailed error information including headers and request details
                        _logger.LogError("Azure Function call failed. Status: {StatusCode}, URL: {Url}, Error: {Error}", 
                            response.StatusCode, 
                            functionUrl.Replace(functionKey, "[REDACTED]"),
                            !string.IsNullOrEmpty(responseContent) ? responseContent : "No error content returned");
                            
                        // Log response headers for debugging
                        foreach (var header in response.Headers)
                        {
                            _logger.LogDebug("Response header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
                        }
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    // Specific handling for HTTP request exceptions
                    _logger.LogError(httpEx, "HTTP request error calling Azure Function: {Message}, URL: {Url}", 
                        httpEx.Message, 
                        functionUrl?.Replace(functionKey ?? "", "[REDACTED]"));
                }
            }
            catch (Exception ex)
            {
                // Log detailed error information but don't fail the main request
                _logger.LogError(ex, "Error saving conversation: {ExceptionType}, Message: {Message}, Stack: {StackTrace}", 
                    ex.GetType().Name,
                    ex.Message,
                    ex.StackTrace?.Split('\n')[0]);
                
                // Log inner exception if present for more context
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {Type}, {Message}", 
                        ex.InnerException.GetType().Name,
                        ex.InnerException.Message);
                }
            }
        }
    }
}
