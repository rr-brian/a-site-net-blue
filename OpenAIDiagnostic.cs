using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;

public class OpenAIDiagnostic
{
    private static IConfiguration _configuration;
    private static string _apiKey;
    private static string _endpoint;
    private static string _deploymentName;
    private static string _apiVersion;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Azure OpenAI Diagnostic Tool ===");
        Console.WriteLine("This tool will help diagnose issues with Azure OpenAI configuration");
        Console.WriteLine();

        // Load configuration
        LoadConfiguration();

        // Display diagnostic information
        DisplayDiagnosticInfo();

        // Test connection with the configured deployment
        await TestConnection();

        // List available deployments (if we have access)
        await ListAvailableDeployments();

        // Test with specific deployment names
        string[] deploymentNamesToTest = new[] 
        { 
            "gpt-4.1", 
            "gpt-4", 
            "gpt-35-turbo" 
        };

        foreach (var deployment in deploymentNamesToTest)
        {
            Console.WriteLine($"\\nTesting with deployment name: {deployment}");
            await TestConnectionWithDeployment(deployment);
        }

        Console.WriteLine("\\nDiagnostic run complete. Press any key to exit.");
        Console.ReadKey();
    }

    private static void LoadConfiguration()
    {
        Console.WriteLine("Loading configuration...");

        try
        {
            // Build configuration from appsettings.json and appsettings.Development.json
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Backend/appsettings.json", optional: true)
                .AddJsonFile("Backend/appsettings.Development.json", optional: true)
                .Build();

            // Try loading from environment variables first
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            _endpoint = Environment.GetEnvironmentVariable("OPENAI_ENDPOINT");
            _deploymentName = Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME");
            _apiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");

            Console.WriteLine("Checking environment variables:");
            Console.WriteLine($"- OPENAI_API_KEY: {(string.IsNullOrEmpty(_apiKey) ? "Not set" : "Set (hidden)")}");
            Console.WriteLine($"- OPENAI_ENDPOINT: {(string.IsNullOrEmpty(_endpoint) ? "Not set" : _endpoint)}");
            Console.WriteLine($"- OPENAI_DEPLOYMENT_NAME: {(string.IsNullOrEmpty(_deploymentName) ? "Not set" : _deploymentName)}");
            Console.WriteLine($"- OPENAI_API_VERSION: {(string.IsNullOrEmpty(_apiVersion) ? "Not set" : _apiVersion)}");

            // Try loading from OpenAI section in appsettings
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(_endpoint))
                _endpoint = _configuration["OpenAI:Endpoint"];
            if (string.IsNullOrEmpty(_deploymentName))
                _deploymentName = _configuration["OpenAI:DeploymentName"];
            if (string.IsNullOrEmpty(_apiVersion))
                _apiVersion = _configuration["OpenAI:ApiVersion"];

            Console.WriteLine("\\nChecking OpenAI section in appsettings:");
            Console.WriteLine($"- OpenAI:ApiKey: {(string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"]) ? "Not set" : "Set (hidden)")}");
            Console.WriteLine($"- OpenAI:Endpoint: {_configuration["OpenAI:Endpoint"] ?? "Not set"}");
            Console.WriteLine($"- OpenAI:DeploymentName: {_configuration["OpenAI:DeploymentName"] ?? "Not set"}");
            Console.WriteLine($"- OpenAI:ApiVersion: {_configuration["OpenAI:ApiVersion"] ?? "Not set"}");

            // Try loading from AzureOpenAI section as fallback
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = _configuration["AzureOpenAI:ApiKey"];
            if (string.IsNullOrEmpty(_endpoint))
                _endpoint = _configuration["AzureOpenAI:Endpoint"];
            if (string.IsNullOrEmpty(_deploymentName))
                _deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            if (string.IsNullOrEmpty(_apiVersion))
                _apiVersion = _configuration["AzureOpenAI:ApiVersion"];

            Console.WriteLine("\\nChecking AzureOpenAI section in appsettings:");
            Console.WriteLine($"- AzureOpenAI:ApiKey: {(string.IsNullOrEmpty(_configuration["AzureOpenAI:ApiKey"]) ? "Not set" : "Set (hidden)")}");
            Console.WriteLine($"- AzureOpenAI:Endpoint: {_configuration["AzureOpenAI:Endpoint"] ?? "Not set"}");
            Console.WriteLine($"- AzureOpenAI:DeploymentName: {_configuration["AzureOpenAI:DeploymentName"] ?? "Not set"}");
            Console.WriteLine($"- AzureOpenAI:ApiVersion: {_configuration["AzureOpenAI:ApiVersion"] ?? "Not set"}");

            // Set defaults if still null
            if (string.IsNullOrEmpty(_deploymentName))
                _deploymentName = "gpt-4.1";
            if (string.IsNullOrEmpty(_apiVersion))
                _apiVersion = "2025-01-01-preview";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
        }
    }

    private static void DisplayDiagnosticInfo()
    {
        Console.WriteLine("\\n=== Diagnostic Information ===");
        Console.WriteLine($"Final configuration values being used:");
        Console.WriteLine($"- API Key: {(string.IsNullOrEmpty(_apiKey) ? "Not set" : $"Set (length: {_apiKey.Length})")}");
        Console.WriteLine($"- Endpoint: {_endpoint ?? "Not set"}");
        Console.WriteLine($"- Deployment Name: {_deploymentName ?? "Not set"}");
        Console.WriteLine($"- API Version: {_apiVersion ?? "Not set"}");
        
        Console.WriteLine("\\nEnvironment Information:");
        Console.WriteLine($"- Current Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"- Operating System: {Environment.OSVersion}");
        Console.WriteLine($"- Runtime Version: {Environment.Version}");
    }

    private static async Task TestConnection()
    {
        Console.WriteLine("\\n=== Testing Connection ===");
        Console.WriteLine($"Testing connection with deployment name: {_deploymentName}");

        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
        {
            Console.WriteLine("ERROR: API Key or Endpoint is not set. Cannot test connection.");
            return;
        }

        try
        {
            // Create OpenAI client
            var client = new OpenAIClient(
                new Uri(_endpoint),
                new AzureKeyCredential(_apiKey));
            
            Console.WriteLine("OpenAI client created successfully.");

            // Try to send a simple completion request
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = _deploymentName,
                Temperature = 0.5f,
                MaxTokens = 100,
                Messages = { new ChatRequestSystemMessage("You are a helpful assistant."), new ChatRequestUserMessage("Hello") }
            };

            Console.WriteLine($"Sending test chat request to deployment: {_deploymentName}");
            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
            
            Console.WriteLine("Chat request successful!");
            Console.WriteLine($"Response: {response.Value.Choices[0].Message.Content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            if (ex is RequestFailedException rfe)
            {
                Console.WriteLine($"Status: {rfe.Status}, Error Code: {rfe.ErrorCode}");
                Console.WriteLine($"Content: {rfe.Message}");
            }
        }
    }

    private static async Task ListAvailableDeployments()
    {
        Console.WriteLine("\\n=== Listing Available Deployments ===");
        
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
        {
            Console.WriteLine("ERROR: API Key or Endpoint is not set. Cannot list deployments.");
            return;
        }

        try
        {
            // This is a bit hacky since the SDK doesn't expose a direct way to list deployments
            // We'll use HttpClient directly to query the Azure OpenAI REST API
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
            
            // Construct the URL for listing deployments
            string baseUrl = _endpoint.TrimEnd('/');
            string apiVersionParam = string.IsNullOrEmpty(_apiVersion) ? "2025-01-01-preview" : _apiVersion;
            string deploymentsUrl = $"{baseUrl}/deployments?api-version={apiVersionParam}";
            
            Console.WriteLine($"Querying deployments at: {deploymentsUrl}");
            
            var response = await httpClient.GetAsync(deploymentsUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Available Deployments:");
                Console.WriteLine(content);
                
                try
                {
                    var jsonDoc = JsonDocument.Parse(content);
                    var deploymentsArray = jsonDoc.RootElement.GetProperty("data");
                    
                    Console.WriteLine("\\nDeployment Names:");
                    foreach (var deployment in deploymentsArray.EnumerateArray())
                    {
                        if (deployment.TryGetProperty("id", out var id))
                        {
                            Console.WriteLine($"- {id}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing deployment data: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to list deployments. Status code: {response.StatusCode}");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async Task TestConnectionWithDeployment(string deploymentName)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint))
        {
            Console.WriteLine("ERROR: API Key or Endpoint is not set. Cannot test connection.");
            return;
        }

        try
        {
            // Create OpenAI client
            var client = new OpenAIClient(
                new Uri(_endpoint),
                new AzureKeyCredential(_apiKey));

            // Try to send a simple completion request
            var chatCompletionsOptions = new ChatCompletionsOptions
            {
                DeploymentName = deploymentName,
                Temperature = 0.5f,
                MaxTokens = 100,
                Messages = { new ChatRequestSystemMessage("You are a helpful assistant."), new ChatRequestUserMessage("Hello") }
            };

            Console.WriteLine($"Sending test chat request to deployment: {deploymentName}");
            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
            
            Console.WriteLine("SUCCESS! Chat request successful with this deployment name.");
            Console.WriteLine($"Response: {response.Value.Choices[0].Message.Content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED with deployment '{deploymentName}': {ex.GetType().Name}: {ex.Message}");
            if (ex is RequestFailedException rfe)
            {
                Console.WriteLine($"Status: {rfe.Status}, Error Code: {rfe.ErrorCode}");
            }
        }
    }
}
