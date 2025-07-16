using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Backend.Services.Interfaces;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiagnosticController> _logger;
        private readonly OpenAIClient _openAIClient;
        private readonly IAzureFunctionService _azureFunctionService;

        public DiagnosticController(
            IConfiguration configuration,
            ILogger<DiagnosticController> logger,
            OpenAIClient openAIClient,
            IAzureFunctionService azureFunctionService = null)
        {
            _configuration = configuration;
            _logger = logger;
            _openAIClient = openAIClient;
            _azureFunctionService = azureFunctionService;
        }

        [HttpGet("openai-config")]
        public IActionResult GetOpenAiConfig()
        {
            // Gather environment variables
            var envApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var envEndpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            var envDeploymentName = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
            var envApiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");

            // Gather OpenAI section from appsettings
            var openAiApiKey = _configuration["OpenAI:ApiKey"];
            var openAiEndpoint = _configuration["OpenAI:Endpoint"];
            var openAiDeploymentName = _configuration["OpenAI:DeploymentName"];
            var openAiApiVersion = _configuration["OpenAI:ApiVersion"];

            // Gather AzureOpenAI section from appsettings
            var azureOpenAiApiKey = _configuration["AzureOpenAI:ApiKey"];
            var azureOpenAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var azureOpenAiDeploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var azureOpenAiApiVersion = _configuration["AzureOpenAI:ApiVersion"];

            // Build result object
            var result = new
            {
                EnvironmentVariables = new
                {
                    OPENAI_API_KEY = string.IsNullOrEmpty(envApiKey) ? "Not set" : "Set (hidden)",
                    OPENAI_ENDPOINT = envEndpoint ?? "Not set",
                    OPENAI_DEPLOYMENT_NAME = envDeploymentName ?? "Not set",
                    OPENAI_API_VERSION = envApiVersion ?? "Not set"
                },
                OpenAI_Section = new
                {
                    ApiKey = string.IsNullOrEmpty(openAiApiKey) ? "Not set" : "Set (hidden)",
                    Endpoint = openAiEndpoint ?? "Not set",
                    DeploymentName = openAiDeploymentName ?? "Not set",
                    ApiVersion = openAiApiVersion ?? "Not set"
                },
                AzureOpenAI_Section = new
                {
                    ApiKey = string.IsNullOrEmpty(azureOpenAiApiKey) ? "Not set" : "Set (hidden)",
                    Endpoint = azureOpenAiEndpoint ?? "Not set",
                    DeploymentName = azureOpenAiDeploymentName ?? "Not set",
                    ApiVersion = azureOpenAiApiVersion ?? "Not set"
                },
                EnvironmentInfo = new
                {
                    CurrentDirectory = System.IO.Directory.GetCurrentDirectory(),
                    OperatingSystem = Environment.OSVersion.ToString(),
                    RuntimeVersion = Environment.Version.ToString()
                }
            };

            return Ok(result);
        }

        [HttpGet("openai-client-info")]
        public IActionResult GetOpenAiClientInfo()
        {
            var clientType = _openAIClient.GetType().FullName;
            var props = _openAIClient.GetType().GetProperties();
            
            var result = new
            {
                ClientType = clientType,
                Properties = props.Select(p => new { Name = p.Name }).ToArray()
            };
            
            return Ok(result);
        }

        [HttpGet("test-deployments")]
        public async Task<IActionResult> TestDeployments()
        {
            // Get configuration first
            var endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT") ?? 
                         _configuration["OpenAI:Endpoint"] ?? 
                         _configuration["AzureOpenAI:Endpoint"] ?? 
                         "unknown";
                         
            var apiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION") ?? 
                           _configuration["OpenAI:ApiVersion"] ?? 
                           _configuration["AzureOpenAI:ApiVersion"] ?? 
                           "2025-01-01-preview";
            
            var results = new List<object>();
            
            // Add configuration information to the results
            results.Add(new
            {
                Type = "ConfigInfo",
                Endpoint = endpoint,
                ApiVersion = apiVersion,
                ClientType = _openAIClient.GetType().FullName
            });
            
            // Deployments to test
            string[] deploymentsToTest = new[] { "gpt-4.1", "gpt-4", "gpt-35-turbo", "gpt-35-turbo-16k" };
            
            foreach (var deploymentName in deploymentsToTest)
            {
                try
                {
                    _logger.LogInformation($"Testing deployment: {deploymentName}");
                    
                    // Construct the full URL that would be used (for diagnostic purposes)
                    var fullUrl = $"{endpoint}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";
                    
                    // Create chat completions options
                    var chatCompletionsOptions = new ChatCompletionsOptions
                    {
                        DeploymentName = deploymentName,
                        Temperature = 0.5f,
                        MaxTokens = 100,
                        Messages = { new ChatRequestSystemMessage("You are a helpful assistant."), new ChatRequestUserMessage("Hello") }
                    };

                    // Send test request
                    _logger.LogInformation($"Sending test request to: {fullUrl}");
                    var response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                    
                    // Success
                    results.Add(new
                    {
                        DeploymentName = deploymentName,
                        Status = "Success",
                        Message = response.Value.Choices[0].Message.Content,
                        FullUrl = fullUrl
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error testing deployment {deploymentName}");
                    
                    string errorCode = "Unknown";
                    string statusCode = "Unknown";
                    string fullUrl = $"{endpoint}/openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}";
                    
                    if (ex is RequestFailedException rfe)
                    {
                        errorCode = rfe.ErrorCode;
                        statusCode = rfe.Status.ToString();
                    }
                    
                    results.Add(new
                    {
                        DeploymentName = deploymentName,
                        Status = "Failed",
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.Message,
                        ErrorCode = errorCode,
                        StatusCode = statusCode,
                        FullUrl = fullUrl
                    });
                }
            }
            
            return Ok(results);
        }

        [HttpGet("azure-function-config")]
        public IActionResult GetAzureFunctionConfig()
        {
            // Gather Azure Function configuration
            var functionUrl = _configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            var functionKey = _configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
            var userId = _configuration["AzureFunction:UserId"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID");
            var userEmail = _configuration["AzureFunction:UserEmail"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL");
            
            // Get appsettings.json and appsettings.Development.json values directly to see if they're properly loaded
            var functionUrlFromAppSettings = _configuration["AzureFunction:Url"];
            var functionKeyFromAppSettings = _configuration["AzureFunction:Key"];
            
            // Get environment variable values
            var functionUrlFromEnvVar = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            var functionKeyFromEnvVar = Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
            
            // Prepare the response
            var result = new
            {
                ConfigurationStatus = new
                {
                    HasUrl = !string.IsNullOrEmpty(functionUrl),
                    HasKey = !string.IsNullOrEmpty(functionKey),
                    HasUserId = !string.IsNullOrEmpty(userId),
                    HasUserEmail = !string.IsNullOrEmpty(userEmail),
                    IsConfigured = !string.IsNullOrEmpty(functionUrl) && !string.IsNullOrEmpty(functionKey)
                },
                UrlDetails = functionUrl != null ? new {
                    Length = functionUrl.Length,
                    StartsWith = functionUrl.Substring(0, Math.Min(20, functionUrl.Length)),
                    ContainsCodeParam = functionUrl.Contains("code="),
                    FullPath = functionUrl.Contains("/conversations/") ? "Contains '/conversations/' path" : "Missing '/conversations/' path"
                } : null,
                ConfigurationSources = new {
                    AppSettings = new {
                        HasUrl = !string.IsNullOrEmpty(functionUrlFromAppSettings),
                        HasKey = !string.IsNullOrEmpty(functionKeyFromAppSettings),
                        UrlStartsWith = !string.IsNullOrEmpty(functionUrlFromAppSettings) ? functionUrlFromAppSettings.Substring(0, Math.Min(20, functionUrlFromAppSettings.Length)) : null
                    },
                    EnvironmentVariables = new {
                        HasUrl = !string.IsNullOrEmpty(functionUrlFromEnvVar),
                        HasKey = !string.IsNullOrEmpty(functionKeyFromEnvVar),
                        UrlStartsWith = !string.IsNullOrEmpty(functionUrlFromEnvVar) ? functionUrlFromEnvVar.Substring(0, Math.Min(20, functionUrlFromEnvVar.Length)) : null
                    }
                }
            };
            
            return Ok(result);
        }
        
        [HttpGet("direct-azure-function-test")]
        public async Task<IActionResult> DirectAzureFunctionTest()
        {
            var results = new List<object>();
            
            try
            {
                _logger.LogWarning("DIRECT TEST: Starting Azure Function direct test");
                
                // Get environment information
                bool isAzureEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
                results.Add(new { Step = "Environment Check", IsAzure = isAzureEnvironment, WebsiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") });
                
                // Check if service is available through DI
                bool serviceAvailable = _azureFunctionService != null;
                results.Add(new { Step = "Service Check", IsAvailable = serviceAvailable, ServiceType = serviceAvailable ? _azureFunctionService.GetType().FullName : "N/A" });
                
                // Get configuration values directly
                var functionUrl = _configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
                var functionKey = _configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                
                results.Add(new { 
                    Step = "Config Check", 
                    HasUrl = !string.IsNullOrEmpty(functionUrl),
                    HasKey = !string.IsNullOrEmpty(functionKey),
                    MaskedUrl = !string.IsNullOrEmpty(functionUrl) ? functionUrl.Replace("/api/", "/****/") : "<not configured>"
                });
                
                if (!serviceAvailable)
                {
                    _logger.LogError("CRITICAL: Azure Function service is not available through DI!");
                    results.Add(new { Step = "ERROR", Message = "Azure Function service is not available through DI" });
                    return Ok(new { Success = false, Results = results });
                }
                
                if (string.IsNullOrEmpty(functionUrl) || string.IsNullOrEmpty(functionKey))
                {
                    _logger.LogError("CRITICAL: Azure Function configuration missing - URL or Key not configured");
                    results.Add(new { Step = "ERROR", Message = "Azure Function configuration missing" });
                    return Ok(new { Success = false, Results = results });
                }
                
                // Directly call Azure Function with test message
                string testUserMessage = "This is a direct test from diagnostic controller";
                string testAiResponse = "This is a simulated AI response for testing";
                
                try
                {
                    _logger.LogWarning("DIRECT TEST: Attempting direct call to Azure Function");
                    results.Add(new { Step = "Direct Call", Status = "Started" });
                    
                    // Create HttpClient directly for testing
                    using (var httpClient = new HttpClient())
                    {
                        // Create the payload with the same structure as AzureFunctionService
                        var userId = _configuration["AzureFunction:UserId"] ?? 
                            Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID") ?? 
                            Guid.NewGuid().ToString();
                            
                        var userEmail = _configuration["AzureFunction:UserEmail"] ?? 
                            Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL") ?? 
                            "test@example.com";
                            
                        var payload = new
                        {
                            userId,
                            userEmail,
                            chatType = "web",
                            messages = new[]
                            {
                                new { role = "user", content = testUserMessage },
                                new { role = "assistant", content = testAiResponse }
                            },
                            totalTokens = 0,
                            metadata = new
                            {
                                source = "web-diagnostic",
                                timestamp = DateTime.UtcNow.ToString("O"),
                                testMode = true
                            }
                        };
                        
                        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                        var jsonContent = System.Text.Json.JsonSerializer.Serialize(payload, jsonOptions);
                        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                        
                        results.Add(new { Step = "Request Preparation", PayloadLength = jsonContent.Length });
                        _logger.LogInformation("DIRECT TEST: Prepared payload: {Payload}", jsonContent);
                        
                        // Add the API key to the URL for direct testing
                        var requestUri = functionUrl;
                        if (!string.IsNullOrEmpty(functionKey))
                        {
                            requestUri += (requestUri.Contains("?") ? "&" : "?") + "code=" + functionKey;
                        }
                        
                        // Set 30 second timeout
                        httpClient.Timeout = TimeSpan.FromSeconds(30);
                        
                        // Make the request with explicit timeout and error handling
                        try
                        {   
                            _logger.LogWarning("DIRECT TEST: Sending HTTP request to {FunctionUrl}", 
                                requestUri.Replace(functionKey, "[REDACTED]"));
                                
                            var response = await httpClient.PostAsync(requestUri, content);
                            var responseContent = await response.Content.ReadAsStringAsync();
                            
                            results.Add(new { 
                                Step = "HTTP Response", 
                                StatusCode = (int)response.StatusCode,
                                Status = response.StatusCode.ToString(),
                                ContentLength = responseContent.Length,
                                Content = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent
                            });
                            
                            _logger.LogInformation("DIRECT TEST: Received response: {Response}", responseContent);
                            
                            if (response.IsSuccessStatusCode)
                            {
                                _logger.LogWarning("DIRECT TEST: Azure Function call successful!");
                                // Try to parse the response for debugging
                                try
                                {
                                    var responseObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseContent);
                                    if (responseObj.TryGetProperty("conversationId", out var conversationId))
                                    {
                                        _logger.LogWarning("DIRECT TEST: Received conversation ID: {ConversationId}", conversationId.ToString());
                                        results.Add(new { Step = "Response Parse", ConversationId = conversationId.ToString() });
                                    }
                                }
                                catch (Exception parseEx)
                                {
                                    _logger.LogError(parseEx, "DIRECT TEST: Error parsing response JSON");
                                    results.Add(new { Step = "Response Parse Error", Error = parseEx.Message });
                                }
                            }
                            else
                            {
                                _logger.LogError("DIRECT TEST: Azure Function returned error status code: {StatusCode}", response.StatusCode);
                            }
                        }
                        catch (TaskCanceledException tcEx)
                        {
                            _logger.LogError(tcEx, "DIRECT TEST: Request timed out after {Timeout} seconds", httpClient.Timeout.TotalSeconds);
                            results.Add(new { Step = "Timeout Error", Error = tcEx.Message, Timeout = httpClient.Timeout.TotalSeconds });
                        }
                        catch (HttpRequestException httpEx)
                        {
                            _logger.LogError(httpEx, "DIRECT TEST: HTTP request error: {Message}", httpEx.Message);
                            results.Add(new { Step = "HTTP Error", Error = httpEx.Message, InnerError = httpEx.InnerException?.Message });
                        }
                        catch (Exception reqEx)
                        {
                            _logger.LogError(reqEx, "DIRECT TEST: Unexpected error during HTTP request: {Message}", reqEx.Message);
                            results.Add(new { Step = "Request Error", Error = reqEx.Message, Type = reqEx.GetType().Name });
                        }
                    }
                    
                    // Now test using the injected service
                    _logger.LogWarning("DIRECT TEST: Testing injected Azure Function service");
                    results.Add(new { Step = "Injected Service Test", Status = "Started" });
                    
                    try
                    {
                        await _azureFunctionService.SaveConversationAsync("Test from injected service", "Test response for injected service");
                        _logger.LogWarning("DIRECT TEST: Injected service call completed successfully");
                        results.Add(new { Step = "Injected Service Test", Status = "Success" });
                    }
                    catch (Exception svcEx)
                    {
                        _logger.LogError(svcEx, "DIRECT TEST: Injected service call failed: {Message}", svcEx.Message);
                        results.Add(new { Step = "Injected Service Error", Error = svcEx.Message, Type = svcEx.GetType().Name });
                    }
                }
                catch (Exception callEx)
                {
                    _logger.LogError(callEx, "DIRECT TEST: Unexpected error in direct test: {Message}", callEx.Message);
                    results.Add(new { Step = "Overall Error", Error = callEx.Message, Type = callEx.GetType().Name });
                }
                
                return Ok(new { Success = true, Results = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DIRECT TEST: Critical error in test endpoint: {Message}", ex.Message);
                results.Add(new { Step = "Critical Error", Error = ex.Message, Type = ex.GetType().Name, StackTrace = ex.StackTrace });
                return StatusCode(500, new { Success = false, Results = results });
            }
        }

        [HttpGet("verify-azure-function-service")]
        public IActionResult VerifyAzureFunctionService()
        {
            try
            {
                bool isAzureEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
                _logger.LogWarning("VERIFICATION: Running in {Environment} environment", isAzureEnvironment ? "Azure" : "Local");
                
                // Check if service is available
                bool serviceAvailable = _azureFunctionService != null;
                _logger.LogWarning("VERIFICATION: Azure Function service available: {Available}", serviceAvailable);
                
                // Get Azure Function configuration directly
                var functionUrl = _configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
                var functionKey = _configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                var userId = _configuration["AzureFunction:UserId"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID");
                var userEmail = _configuration["AzureFunction:UserEmail"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL");
                
                var configStatus = new
                {
                    InAzureEnvironment = isAzureEnvironment,
                    ServiceAvailable = serviceAvailable,
                    ServiceType = serviceAvailable ? _azureFunctionService.GetType().FullName : "N/A",
                    ConfigFound = new
                    {
                        UrlConfigured = !string.IsNullOrEmpty(functionUrl),
                        KeyConfigured = !string.IsNullOrEmpty(functionKey),
                        UserIdConfigured = !string.IsNullOrEmpty(userId),
                        UserEmailConfigured = !string.IsNullOrEmpty(userEmail)
                    },
                    ConfigSources = new
                    {
                        UrlFromEnvVar = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL")),
                        UrlFromAppSettings = !string.IsNullOrEmpty(_configuration["AzureFunction:Url"]),
                        KeyFromEnvVar = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY")),
                        KeyFromAppSettings = !string.IsNullOrEmpty(_configuration["AzureFunction:Key"])
                    },
                    MaskedUrl = !string.IsNullOrEmpty(functionUrl) ? functionUrl.Replace("/api/", "/****/") : "<not configured>"
                };
                
                return Ok(configStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Azure Function service");
                return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        [HttpPost("test-azure-function")]
        public async Task<IActionResult> TestAzureFunction()
        {
            try
            {
                // Gather Azure Function configuration
                var functionUrl = _configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
                var functionKey = _configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");
                var userId = _configuration["AzureFunction:UserId"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID") ?? $"user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var userEmail = _configuration["AzureFunction:UserEmail"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL") ?? $"user-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}@realtyts.com";
                
                if (string.IsNullOrEmpty(functionUrl) || string.IsNullOrEmpty(functionKey))
                {
                    return BadRequest("Azure Function URL or key is not configured.");
                }
                
                // Create test data
                var userMessage = "test user message from diagnostic api";
                var aiResponse = "test ai response from diagnostic api";
                var conversationId = Guid.NewGuid().ToString();
                
                using var httpClient = new HttpClient();
                
                // Create result object to track both service test and direct HTTP test
                dynamic resultData = new {
                    ServiceTest = new { Status = "Started", ErrorMessage = (string)null },
                    DirectHttpTest = new { Status = "Started", ErrorMessage = (string)null, StatusCode = (int?)null },
                    DirectHttpManualTest = new { Status = "Started", ErrorMessage = (string)null, StatusCode = (int?)null },
                    Configuration = new { 
                        HasUrl = !string.IsNullOrEmpty(functionUrl),
                        HasKey = !string.IsNullOrEmpty(functionKey),
                        UrlStartsWith = !string.IsNullOrEmpty(functionUrl) ? functionUrl.Substring(0, Math.Min(20, functionUrl.Length)) : null,
                        FullUrl = functionUrl + (functionUrl.Contains("?") ? "&" : "?") + "code=" + (functionKey?.Length > 10 ? functionKey.Substring(0, 3) + "..." : "[no-key]")
                    }
                };
                
                // Test using the injected Azure Function service
                try
                {
                    await _azureFunctionService.SaveConversationAsync(userMessage, aiResponse);
                    resultData = new { 
                        ServiceTest = new { Status = "Success", ErrorMessage = (string)null },
                        DirectHttpTest = resultData.DirectHttpTest,
                        DirectHttpManualTest = resultData.DirectHttpManualTest,
                        Configuration = resultData.Configuration
                    };
                }
                catch (Exception ex)
                {
                    resultData = new { 
                        ServiceTest = new { Status = "Failed", ErrorMessage = ex.Message },
                        DirectHttpTest = resultData.DirectHttpTest,
                        DirectHttpManualTest = resultData.DirectHttpManualTest,
                        Configuration = resultData.Configuration
                    };
                }
                
                // 2. Direct HTTP test with standard format
                try
                {
                    // Build the URL with the function key
                    var requestUri = functionUrl;
                    if (!string.IsNullOrEmpty(functionKey) && !requestUri.Contains("code="))
                    {
                        requestUri = requestUri + (requestUri.Contains("?") ? "&" : "?") + "code=" + functionKey;
                    }
                    
                    // Create the standard format payload (matching what the AzureFunctionService sends)
                    var testConversationId = Guid.NewGuid().ToString();
                    var testPayload = new
                    {
                        conversationId = testConversationId,
                        userId = userId,  // Use the configured userId
                        userEmail = userEmail,  // Use the configured userEmail
                        chatType = "diagnostic-test",
                        messages = new[]
                        {
                            new { role = "user", content = userMessage },
                            new { role = "assistant", content = aiResponse }
                        },
                        totalTokens = 0,
                        metadata = new
                        {
                            source = "diagnostic-test",
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    };
                    
                    // Log the exact JSON we're sending for debugging
                    var jsonContent = JsonSerializer.Serialize(testPayload, new JsonSerializerOptions { WriteIndented = true });
                    _logger.LogInformation("Sending payload to Azure Function: {JsonContent}", jsonContent);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    
                    _logger.LogInformation("Sending request to: {RequestUri}", requestUri.Replace(functionKey, "[REDACTED]"));
                    var response = await httpClient.PostAsync(requestUri, content);
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Azure Function direct HTTP test succeeded with response: {Response}", responseContent);
                        resultData = new { 
                            ServiceTest = resultData.ServiceTest,
                            DirectHttpTest = new { 
                                Status = "Success", 
                                StatusCode = (int)response.StatusCode,
                                ErrorMessage = (string)null,
                                Response = responseContent,
                                ConversationId = testConversationId
                            },
                            DirectHttpManualTest = resultData.DirectHttpManualTest,
                            Configuration = resultData.Configuration
                        };
                    }
                    else
                    {
                        _logger.LogError("Azure Function direct HTTP test failed with status {StatusCode}: {Response}", 
                            response.StatusCode, responseContent);
                        resultData = new { 
                            ServiceTest = resultData.ServiceTest,
                            DirectHttpTest = new { 
                                Status = "Failed", 
                                StatusCode = (int)response.StatusCode,
                                ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseContent}" 
                            },
                            DirectHttpManualTest = resultData.DirectHttpManualTest,
                            Configuration = resultData.Configuration
                        };
                    }
                    
                    // 3. Try with an alternative payload format
                    try
                    {
                        // Try a simpler format that might be more compatible with the Azure Function
                        var simplePayload = new
                        {
                            id = Guid.NewGuid().ToString(),
                            user = new { id = userId, email = userEmail },
                            conversation = new[]
                            {
                                new { sender = "user", text = userMessage, timestamp = DateTime.UtcNow.AddSeconds(-10).ToString("o") },
                                new { sender = "assistant", text = aiResponse, timestamp = DateTime.UtcNow.ToString("o") }
                            }
                        };
                        
                        var simpleJsonContent = JsonSerializer.Serialize(simplePayload, new JsonSerializerOptions { WriteIndented = true });
                        _logger.LogInformation("Sending alternative payload format to Azure Function: {JsonContent}", simpleJsonContent);
                        var simpleContent = new StringContent(simpleJsonContent, Encoding.UTF8, "application/json");
                        
                        var simpleResponse = await httpClient.PostAsync(requestUri, simpleContent);
                        var simpleResponseContent = await simpleResponse.Content.ReadAsStringAsync();
                        
                        if (simpleResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Azure Function alternative format test succeeded with response: {Response}", simpleResponseContent);
                            resultData = new { 
                                ServiceTest = resultData.ServiceTest,
                                DirectHttpTest = resultData.DirectHttpTest,
                                DirectHttpManualTest = new { 
                                    Status = "Success", 
                                    StatusCode = (int)simpleResponse.StatusCode,
                                    ErrorMessage = (string)null,
                                    Response = simpleResponseContent
                                },
                                Configuration = resultData.Configuration
                            };
                        }
                        else
                        {
                            _logger.LogError("Azure Function alternative format test failed with status {StatusCode}: {Response}", 
                                simpleResponse.StatusCode, simpleResponseContent);
                            resultData = new { 
                                ServiceTest = resultData.ServiceTest,
                                DirectHttpTest = resultData.DirectHttpTest,
                                DirectHttpManualTest = new { 
                                    Status = "Failed", 
                                    StatusCode = (int)simpleResponse.StatusCode,
                                    ErrorMessage = $"HTTP {(int)simpleResponse.StatusCode}: {simpleResponseContent}" 
                                },
                                Configuration = resultData.Configuration
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in alternative format test");
                        resultData = new { 
                            ServiceTest = resultData.ServiceTest,
                            DirectHttpTest = resultData.DirectHttpTest,
                            DirectHttpManualTest = new { 
                                Status = "Failed", 
                                StatusCode = (int?)null,
                                ErrorMessage = $"Alternative format error: {ex.Message}" 
                            },
                            Configuration = resultData.Configuration
                        };
                    }
                }
                catch (Exception ex)
                {
                    resultData = new { 
                        ServiceTest = resultData.ServiceTest,
                        DirectHttpTest = new { Status = "Failed", ErrorMessage = ex.Message, StatusCode = (int?)null },
                        DirectHttpManualTest = new { Status = "Failed", ErrorMessage = ex.Message, StatusCode = (int?)null },
                        Configuration = resultData.Configuration
                    };
                }
                
                return Ok(resultData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }
    }
}
