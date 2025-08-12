using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Azure;
using Azure.AI.OpenAI;
using Backend.Configuration;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    /// <summary>
    /// Main service for processing chat requests with or without document context
    /// </summary>
    public class ChatService : Interfaces.IChatService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly OpenAIConfiguration _openAIConfig;
        private readonly Interfaces.IDocumentChunkingService _documentChunkingService;
        private readonly Interfaces.IDocumentContextService _documentContextService;
        private readonly Interfaces.IChatAnalysisService _chatAnalysisService;
        private readonly Interfaces.IPromptEngineeringService _promptEngineeringService;
        private readonly Interfaces.IAzureFunctionService _azureFunctionService;
        private readonly ILogger<ChatService> _logger;
        
        // Default retry settings for API call failures
        private const int MaxRetries = 3;
        private const int InitialRetryDelayMs = 1000;
        
        public ChatService(
            OpenAIClient openAIClient,
            OpenAIConfiguration openAIConfig,
            Interfaces.IDocumentChunkingService documentChunkingService,
            Interfaces.IDocumentContextService documentContextService,
            Interfaces.IChatAnalysisService chatAnalysisService,
            Interfaces.IPromptEngineeringService promptEngineeringService,
            ILogger<ChatService> logger,
            Interfaces.IAzureFunctionService azureFunctionService = null)
        {
            _openAIClient = openAIClient;
            _openAIConfig = openAIConfig;
            _documentChunkingService = documentChunkingService;
            _documentContextService = documentContextService;
            _chatAnalysisService = chatAnalysisService;
            _promptEngineeringService = promptEngineeringService;
            _azureFunctionService = azureFunctionService;
            _logger = logger;
        }
        
        /// <summary>
        /// Process a chat request with or without document context
        /// </summary>
        /// <param name="message">The message from the user</param>
        /// <param name="documentInfo">Optional document context information</param>
        /// <returns>The AI response text</returns>
        public async Task<string> ProcessChatRequest(string message, DocumentInfo? documentInfo = null)
        {
            try
            {
                // Check if OpenAI client is available
                if (_openAIClient == null)
                {
                    _logger.LogWarning("OpenAI client is not available. Check Azure OpenAI configuration.");
                    return "I'm sorry, but the AI service is not currently available. Please check your Azure OpenAI configuration and try again later.";
                }
                
                // Add detailed OpenAI connection debugging
                _logger.LogWarning("DEBUG - OpenAI API Connection Details:");
                _logger.LogWarning("OpenAI Endpoint: {Endpoint}", _openAIConfig.Endpoint ?? "<null>");
                _logger.LogWarning("OpenAI API Key Length: {Length}", _openAIConfig.ApiKey?.Length ?? 0);
                _logger.LogWarning("OpenAI IsConfigured: {IsConfigured}", _openAIConfig.IsConfigured);
                
                // Get deployment name from configuration
                var deploymentName = _openAIConfig.DeploymentName ?? "gpt-4.1";  // This must match the deployment in Azure OpenAI
                
                // Add extensive logging of all parameters
                _logger.LogWarning("API CALL DETAILS - Endpoint: {Endpoint}", _openAIConfig.Endpoint);
                _logger.LogWarning("API CALL DETAILS - DeploymentName: {DeploymentName}", deploymentName);
                var apiVersion = Environment.GetEnvironmentVariable("OPENAI_API_VERSION");
                _logger.LogWarning("API CALL DETAILS - API Version: {ApiVersion}", apiVersion ?? "Not set in env vars");
                
                _logger.LogInformation("Using deployment name: {DeploymentName}", deploymentName);
                _logger.LogInformation("Processing chat request with message: {MessageLength} chars, Document: {HasDocument}", 
                    message?.Length ?? 0, documentInfo != null);
                
                // Log detailed API call parameters
                _logger.LogWarning("DETAILED OPENAI DIAGNOSTICS:");
                _logger.LogWarning("1. Using OpenAIClient of type: {ClientType}", _openAIClient.GetType().FullName);
                _logger.LogWarning("2. Endpoint URL being used: {EndpointUrl}", _openAIConfig.Endpoint);
                _logger.LogWarning("3. Deployment name being used: {DeploymentName}", deploymentName);
                _logger.LogWarning("4. Environment variables:");
                _logger.LogWarning("   OPENAI_API_KEY set: {IsSet}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
                _logger.LogWarning("   OPENAI_ENDPOINT set: {IsSet}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_ENDPOINT")));
                _logger.LogWarning("   OPENAI_DEPLOYMENT_NAME set: {IsSet}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_DEPLOYMENT_NAME")));
                
                // Setup chat completion options
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = deploymentName,
                    Temperature = 0.5f,
                    MaxTokens = 4000,
                };
                
                // Prepare document context if available
                string documentContext = null;
                string systemPrompt = null;
                if (documentInfo != null)
                {
                    _logger.LogInformation("Processing chat with document context. Document: {FileName}", documentInfo.FileName);
                    
                    // Add detailed logging about document content
                    _logger.LogWarning("DOCUMENT DEBUG: Document {FileName} has {ChunkCount} chunks and {MetadataCount} metadata items", 
                        documentInfo.FileName,
                        documentInfo.Chunks?.Count ?? 0,
                        documentInfo.ChunkMetadata?.Count ?? 0);
                        
                    if (documentInfo.Chunks == null || documentInfo.Chunks.Count == 0)
                    {
                        _logger.LogError("CRITICAL ERROR: Document info exists but has NO CHUNKS - this will result in empty context");
                        
                        // This is critical - if we somehow have a DocumentInfo but it has no chunks, 
                        // the user will see the document context indicator but the AI won't have access to the document
                        _logger.LogError("Document info has no chunks - this is likely a serialization or persistence issue");
                    }
                    else
                    {
                        _logger.LogInformation("Document chunk sample: {Sample}", 
                            documentInfo.Chunks[0].Length > 50 
                                ? documentInfo.Chunks[0].Substring(0, 50) + "..." 
                                : documentInfo.Chunks[0]);
                    }
                    
                    // Use the DocumentContextService to prepare document context
                    documentContext = _documentContextService.PrepareDocumentContext(documentInfo, message);
                    _logger.LogInformation("Document context prepared: {Length} chars", documentContext?.Length ?? 0);
                    
                    // Verify we have actual document context content
                    if (string.IsNullOrEmpty(documentContext))
                    {
                        _logger.LogError("CRITICAL ERROR: Document context is empty despite having document info");
                    }
                    else
                    {
                        _logger.LogInformation("Document context sample: {Sample}", 
                            documentContext.Length > 50 
                                ? documentContext.Substring(0, 50) + "..." 
                                : documentContext);
                    }
                    
                    // Generate system prompt for document context
                    systemPrompt = _promptEngineeringService.CreateSystemPrompt(documentInfo, documentContext);
                }
                
                // Use the provided system prompt if available, otherwise create one
                if (systemPrompt == null)
                {
                    _logger.LogInformation("No custom system prompt provided, generating one with PromptEngineeringService");
                    systemPrompt = _promptEngineeringService.CreateSystemPrompt(documentInfo, documentContext);
                }
                else
                {
                    _logger.LogInformation("Using provided custom system prompt: {Length} characters", systemPrompt.Length);
                }
                
                // Add system message
                chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
                
                // Add user message
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message));
                
                // Call OpenAI API with retry logic
                Response<ChatCompletions> response = null;
                int retryCount = 0;
                int retryDelay = InitialRetryDelayMs;
                
                while (retryCount <= MaxRetries)
                {
                    try 
                    {
                        _logger.LogInformation("Sending chat request to OpenAI API (attempt {Attempt})", retryCount + 1);
                        response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                        break; // Success, exit retry loop
                    }
                    catch (RequestFailedException ex) when (ex.Status == 429) // Too Many Requests
                    {
                        retryCount++;
                        
                        if (retryCount > MaxRetries)
                        {
                            _logger.LogError(ex, "Failed to get chat completions after {Retries} retries due to rate limits", MaxRetries);
                            throw;
                        }
                        
                        _logger.LogWarning("Rate limit exceeded (429). Retrying in {Delay}ms. Attempt {Attempt} of {MaxRetries}", 
                            retryDelay, retryCount, MaxRetries);
                            
                        await Task.Delay(retryDelay);
                        retryDelay *= 2; // Exponential backoff
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calling OpenAI API: {ErrorMessage}", ex.Message);
                        throw;
                    }
                }
                
                // Extract response text and log completion
                if (response?.Value?.Choices?.Count > 0)
                {
                    string responseText = response.Value.Choices[0].Message.Content;
                    _logger.LogInformation("Received response from OpenAI API: {Length} chars", responseText?.Length ?? 0);
                    
                    // Save conversation to Azure Function if service is available
                    if (_azureFunctionService != null)
                    {
                        try
                        {
                            // Log whether this is Azure environment or local
                            bool isAzureEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
                            _logger.LogWarning("AZURE FUNCTION DEBUG: Running in {Environment} environment", 
                                isAzureEnvironment ? "Azure" : "Local");
                                
                            // Log all Azure Function configuration values (with sensitive data masked)
                            var functionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL") ?? 
                                "[NOT SET IN ENVIRONMENT]";
                            var hasFunctionKey = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY"));
                            var hasUserId = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_ID"));
                            var hasUserEmail = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTION_USER_EMAIL"));
                            
                            _logger.LogWarning("AZURE FUNCTION CONFIG: URL={Url}, HasKey={HasKey}, HasUserId={HasUserId}, HasUserEmail={HasUserEmail}",
                                functionUrl.Replace("/api/", "/***/"), // Mask part of the URL
                                hasFunctionKey,
                                hasUserId,
                                hasUserEmail);
                                
                            // Change to an explicit async method that will be awaited internally
                            // to ensure it runs to completion even during AppDomain shutdown
                            _logger.LogInformation("Saving conversation to Azure Function");
                            
                            // Create a separate task to handle the async operation
                            var saveTask = Task.Run(async () => 
                            {
                                try
                                {
                                    // Force synchronous context to ensure completion
                                    await Task.Yield();
                                    
                                    // Create a unique ID for this save operation for tracking
                                    var saveId = Guid.NewGuid().ToString().Substring(0, 8);
                                    _logger.LogWarning("AZURE_FUNCTION_SAVE_START [{SaveId}]: Starting conversation save", saveId);
                                    
                                    // Use a custom timeout to prevent hanging
                                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                                    {
                                        try
                                        {
                                            await _azureFunctionService.SaveConversationAsync(message, responseText)
                                                .WaitAsync(cts.Token)
                                                .ConfigureAwait(false);
                                                
                                            _logger.LogWarning("AZURE_FUNCTION_SAVE_SUCCESS [{SaveId}]: Successfully saved conversation", saveId);
                                        }
                                        catch (TaskCanceledException)
                                        {
                                            _logger.LogError("AZURE_FUNCTION_SAVE_TIMEOUT [{SaveId}]: Azure Function call timed out after 30 seconds", saveId);
                                        }
                                        catch (Exception innerEx)
                                        {
                                            _logger.LogError(innerEx, "AZURE_FUNCTION_SAVE_ERROR [{SaveId}]: Inner exception: {Type}, Message: {Message}", 
                                                saveId, innerEx.GetType().Name, innerEx.Message);
                                        }
                                    }
                                }
                                catch (Exception outerEx)
                                {
                                    _logger.LogError(outerEx, "Failed to save conversation to Azure Function: {Message}", outerEx.Message);
                                }
                            });
                            
                            // Ensure the background task continues but doesn't block response
                            _ = saveTask;
                        }
                        catch (Exception ex)
                        {
                            // Log but don't fail the request if conversation saving fails
                            _logger.LogError(ex, "Error initiating conversation save to Azure Function: {Message}, Type: {Type}", 
                                ex.Message, ex.GetType().Name);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Azure Function service is NULL - conversation will not be saved");
                    }
                    
                    return responseText;
                }
                
                // If we got here, something went wrong
                _logger.LogWarning("No valid response received from OpenAI API");
                return "I'm sorry, but I couldn't generate a response. Please try again later.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                return "I'm sorry, an error occurred while processing your request. Please try again later.";
            }
        }

        /// <summary>
        /// Process a chat request with conversation history and optional document context
        /// </summary>
        /// <param name="message">The current message from the user</param>
        /// <param name="conversationHistory">Previous conversation messages</param>
        /// <param name="documentInfo">Optional document context information</param>
        /// <returns>The AI response text</returns>
        public async Task<string> ProcessChatRequestWithHistory(string message, List<ChatHistoryMessage> conversationHistory, DocumentInfo? documentInfo = null)
        {
            try
            {
                // Check if OpenAI client is available
                if (_openAIClient == null)
                {
                    _logger.LogWarning("OpenAI client is not available. Check Azure OpenAI configuration.");
                    return "I'm sorry, but the AI service is not currently available. Please check your Azure OpenAI configuration and try again later.";
                }
                
                _logger.LogInformation("Processing chat request with conversation history: {HistoryCount} messages, Document: {HasDocument}", 
                    conversationHistory?.Count ?? 0, documentInfo != null);
                
                // Get deployment name from configuration
                var deploymentName = _openAIConfig.DeploymentName ?? "gpt-35-turbo";
                
                // Setup chat completion options
                var chatCompletionsOptions = new ChatCompletionsOptions
                {
                    DeploymentName = deploymentName,
                    Temperature = 0.5f,
                    MaxTokens = 4000,
                };
                
                // Prepare document context if available
                string documentContext = null;
                string systemPrompt = null;
                if (documentInfo != null)
                {
                    _logger.LogInformation("Processing chat with document context. Document: {FileName}", documentInfo.FileName);
                    
                    // Use the DocumentContextService to prepare document context
                    documentContext = _documentContextService.PrepareDocumentContext(documentInfo, message);
                    _logger.LogInformation("Document context prepared: {Length} chars", documentContext?.Length ?? 0);
                    
                    // Generate system prompt for document context
                    systemPrompt = _promptEngineeringService.CreateSystemPrompt(documentInfo, documentContext);
                }
                
                // Use the provided system prompt if available, otherwise create one
                if (systemPrompt == null)
                {
                    _logger.LogInformation("No custom system prompt provided, generating one with PromptEngineeringService");
                    systemPrompt = _promptEngineeringService.CreateSystemPrompt(documentInfo, documentContext);
                }
                
                // Add system message
                chatCompletionsOptions.Messages.Add(new ChatRequestSystemMessage(systemPrompt));
                
                // Add conversation history
                if (conversationHistory != null && conversationHistory.Count > 0)
                {
                    _logger.LogInformation("Adding {HistoryCount} messages from conversation history", conversationHistory.Count);
                    
                    foreach (var historyMessage in conversationHistory)
                    {
                        if (historyMessage.Role.ToLower() == "user")
                        {
                            chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(historyMessage.Content));
                        }
                        else if (historyMessage.Role.ToLower() == "assistant")
                        {
                            chatCompletionsOptions.Messages.Add(new ChatRequestAssistantMessage(historyMessage.Content));
                        }
                    }
                }
                
                // Add current user message
                chatCompletionsOptions.Messages.Add(new ChatRequestUserMessage(message));
                
                // Call OpenAI API with retry logic
                Response<ChatCompletions> response = null;
                int retryCount = 0;
                int retryDelay = InitialRetryDelayMs;
                
                while (retryCount <= MaxRetries)
                {
                    try 
                    {
                        _logger.LogInformation("Sending chat request to OpenAI API (attempt {Attempt}) with {MessageCount} messages", 
                            retryCount + 1, chatCompletionsOptions.Messages.Count);
                        response = await _openAIClient.GetChatCompletionsAsync(chatCompletionsOptions);
                        break; // Success, exit retry loop
                    }
                    catch (RequestFailedException ex) when (ex.Status == 429) // Too Many Requests
                    {
                        retryCount++;
                        
                        if (retryCount > MaxRetries)
                        {
                            _logger.LogError(ex, "Failed to get chat completions after {Retries} retries due to rate limits", MaxRetries);
                            throw;
                        }
                        
                        _logger.LogWarning("Rate limit exceeded (429). Retrying in {Delay}ms. Attempt {Attempt} of {MaxRetries}", 
                            retryDelay, retryCount, MaxRetries);
                            
                        await Task.Delay(retryDelay);
                        retryDelay *= 2; // Exponential backoff
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calling OpenAI API on attempt {Attempt}: {Message}", retryCount + 1, ex.Message);
                        throw;
                    }
                }
                
                if (response?.Value?.Choices?.Count > 0)
                {
                    string responseText = response.Value.Choices[0].Message.Content;
                    _logger.LogInformation("Received response from OpenAI API: {Length} chars", responseText?.Length ?? 0);
                    
                    // Save conversation to Azure Function if service is available
                    if (_azureFunctionService != null)
                    {
                        try
                        {
                            // Fire and forget - don't wait for this to complete
                            var saveTask = Task.Run(async () =>
                            {
                                try
                                {
                                    var saveId = Guid.NewGuid().ToString().Substring(0, 8);
                                    _logger.LogWarning("AZURE_FUNCTION_SAVE_START [{SaveId}]: Starting conversation save", saveId);
                                    
                                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                                    {
                                        try
                                        {
                                            await _azureFunctionService.SaveConversationAsync(message, responseText)
                                                .WaitAsync(cts.Token)
                                                .ConfigureAwait(false);
                                                
                                            _logger.LogWarning("AZURE_FUNCTION_SAVE_SUCCESS [{SaveId}]: Successfully saved conversation", saveId);
                                        }
                                        catch (TaskCanceledException)
                                        {
                                            _logger.LogError("AZURE_FUNCTION_SAVE_TIMEOUT [{SaveId}]: Azure Function call timed out after 30 seconds", saveId);
                                        }
                                        catch (Exception innerEx)
                                        {
                                            _logger.LogError(innerEx, "AZURE_FUNCTION_SAVE_ERROR [{SaveId}]: Inner exception: {Type}, Message: {Message}", 
                                                saveId, innerEx.GetType().Name, innerEx.Message);
                                        }
                                    }
                                }
                                catch (Exception outerEx)
                                {
                                    _logger.LogError(outerEx, "Failed to save conversation to Azure Function: {Message}", outerEx.Message);
                                }
                            });
                            
                            _ = saveTask;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error initiating conversation save to Azure Function: {Message}, Type: {Type}", 
                                ex.Message, ex.GetType().Name);
                        }
                    }
                    
                    return responseText;
                }
                
                _logger.LogWarning("No valid response received from OpenAI API");
                return "I'm sorry, but I couldn't generate a response. Please try again later.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request with history");
                return "I'm sorry, an error occurred while processing your request. Please try again later.";
            }
        }

        /// <summary>
        /// Process a chat request with a newly uploaded document
        /// </summary>
        public async Task<(string Response, DocumentInfo DocumentInfo)> ProcessChatWithDocument(
            string userMessage, 
            string documentText, 
            string fileName)
        {
            try
            {
                _logger.LogInformation("Starting ProcessChatWithDocument with file {FileName}, document text length: {TextLength}", 
                    fileName, documentText?.Length ?? 0);
                
                if (string.IsNullOrEmpty(documentText))
                {
                    _logger.LogWarning("Empty document text received for file {FileName}", fileName);
                    var emptyDocInfo = new DocumentInfo { FileName = fileName, UploadTime = DateTime.Now };
                    return ("The uploaded document appears to be empty or could not be processed. Please try uploading a different file.", emptyDocInfo);
                }
                
                // Create document chunks directly
                _logger.LogInformation("Chunking document {FileName}", fileName);
                var chunks = _documentChunkingService.ChunkDocument(documentText);
                
                _logger.LogInformation("Document {FileName} chunked into {ChunkCount} chunks", fileName, chunks.Count);
                
                // Create document info
                var documentInfo = new DocumentInfo
                {
                    FileName = fileName,
                    Chunks = chunks,
                    UploadTime = DateTime.Now
                };
                
                // Process chat with document context
                _logger.LogInformation("Processing chat with document context for {FileName}", fileName);
                
                // Use our specialized services to enhance the context
                string documentContext = _documentContextService.PrepareDocumentContext(documentInfo, userMessage);
                
                _logger.LogInformation("Created document context ({ContextLength} chars)", 
                    documentContext?.Length ?? 0);
                
                // Process chat with the document context
                // Make sure we use the correct parameter name to match the ProcessChatRequest method signature
                string response = await ProcessChatRequest(message: userMessage, documentInfo: documentInfo);
                
                return (response, documentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat with document {FileName}: {ErrorMessage}", fileName, ex.Message);
                
                // Create minimal document info for response
                var errorDocInfo = new DocumentInfo { FileName = fileName, UploadTime = DateTime.Now };
                
                // Return a user-friendly error message
                return ($"An error occurred while processing your document: {ex.Message}", errorDocInfo);
            }
        }
    }
}
