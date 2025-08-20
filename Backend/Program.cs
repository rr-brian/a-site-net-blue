using Microsoft.AspNetCore.Server.IIS;
using Backend.Services;
using Backend.Services.Interfaces;
using Backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options => {
    // Increase the max model binding size limit for file uploads
    options.MaxModelBindingCollectionSize = 10000;
});

// Configure request size limits for file uploads (matching web.config)
builder.Services.Configure<IISServerOptions>(options => {
    options.MaxRequestBodySize = 52428800; // 50 MB in bytes
});

// Configure Kestrel server limits
builder.WebHost.ConfigureKestrel(options => {
    options.Limits.MaxRequestBodySize = 52428800; // 50 MB in bytes
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClient factory
builder.Services.AddHttpClient();

// Add session support with enhanced configuration for document persistence
builder.Services.AddDistributedMemoryCache(options => {
    // Increase cache size limit for storing larger documents
    options.SizeLimit = 100 * 1024 * 1024; // 100 MB
});

builder.Services.AddSession(options =>
{
    // Extend session timeout to handle longer conversations
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Ensure cookie works with same-origin requests
    options.Cookie.Name = "RAI_SESSION"; // Give the session cookie a specific name
    // Increase max session size to accommodate larger documents
    options.IOTimeout = TimeSpan.FromSeconds(60);
});

// Register core services
builder.Services.AddTransient<Backend.Services.Interfaces.IChatService, Backend.Services.ChatService>();
builder.Services.AddSingleton<Backend.Services.Interfaces.IDocumentPersistenceService, Backend.Services.DocumentPersistenceService>();

// Register controllers for DI resolution
builder.Services.AddScoped<Backend.Controllers.DocumentChatController>();
builder.Services.AddSingleton<Backend.Services.Interfaces.ISemanticChunker, Backend.Services.SemanticChunker>();
builder.Services.AddTransient<Backend.Services.Interfaces.IDocumentProcessingService, Backend.Services.DocumentProcessingService>();
builder.Services.AddSingleton<Backend.Services.Interfaces.IDocumentChunkingService, Backend.Services.DocumentChunkingService>();
builder.Services.AddSingleton<Backend.Services.Interfaces.IDocumentSearchService, Backend.Services.DocumentSearchService>();

// Register specialized refactored services
builder.Services.AddScoped<Backend.Services.Interfaces.IDocumentContextService, Backend.Services.DocumentContextService>();
builder.Services.AddScoped<Backend.Services.Interfaces.IChatAnalysisService, Backend.Services.ChatAnalysisService>();
builder.Services.AddScoped<Backend.Services.Interfaces.IPromptEngineeringService, Backend.Services.PromptEngineeringService>();

// Register new maintainability services
builder.Services.AddScoped<Backend.Services.Interfaces.IFileValidationService, Backend.Services.FileValidationService>();
builder.Services.AddScoped<Backend.Services.Interfaces.IRequestDiagnosticsService, Backend.Services.RequestDiagnosticsService>();
builder.Services.AddScoped<Backend.Services.Interfaces.ILegacyEndpointHandler, Backend.Services.LegacyEndpointHandler>();
builder.Services.AddScoped<Backend.Services.Interfaces.IOpenAIService, Backend.Services.OpenAIService>();
// Register and configure Azure Function service with explicit validation
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogWarning("Registering Azure Function service with configuration checks");

// Check if Azure Function configuration is available
var functionUrl = builder.Configuration["AzureFunction:Url"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
var functionKey = builder.Configuration["AzureFunction:Key"] ?? Environment.GetEnvironmentVariable("AZURE_FUNCTION_KEY");

// Log configuration status during startup
logger.LogWarning("STARTUP: Azure Function URL configured: {IsConfigured}, Key configured: {HasKey}", 
    !string.IsNullOrEmpty(functionUrl),
    !string.IsNullOrEmpty(functionKey));

// Register the service
builder.Services.AddScoped<Backend.Services.Interfaces.IAzureFunctionService, Backend.Services.AzureFunctionService>();

// Add Entra ID Authentication
builder.Services.AddEntraIdAuthentication(builder.Configuration);

// Add OpenAI Configuration
builder.Services.AddSingleton<Backend.Configuration.OpenAIConfiguration>();

// Define a null object implementation of OpenAIClient for when config is missing
builder.Services.AddSingleton<Azure.AI.OpenAI.OpenAIClient>(sp => 
{
    var openAIConfig = sp.GetRequiredService<Backend.Configuration.OpenAIConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    try
    {
        if (!openAIConfig.IsConfigured)
        {
            logger.LogWarning("OpenAI configuration is missing or incomplete. Some features will not work.");
            
            // Use an empty endpoint that will fail gracefully rather than returning null
            return new Azure.AI.OpenAI.OpenAIClient(
                new Uri("https://dummy-endpoint.openai.azure.com/"),
                new Azure.AzureKeyCredential("dummy-key-for-null-implementation"));
        }
        
        logger.LogInformation("Initializing Azure OpenAI client with endpoint: {Endpoint}", openAIConfig.Endpoint);

        try 
        {
            // Create the client using our configuration adapter
            var client = new Azure.AI.OpenAI.OpenAIClient(
                new Uri(openAIConfig.Endpoint),
                new Azure.AzureKeyCredential(openAIConfig.ApiKey));
            
            logger.LogInformation("Successfully created OpenAI client");
            return client;
        }
        catch (Exception innerEx) 
        {
            logger.LogError(innerEx, "Error creating OpenAI client: {Message}", innerEx.Message);
            
            // Use an empty endpoint that will fail gracefully rather than returning null
            return new Azure.AI.OpenAI.OpenAIClient(
                new Uri("https://dummy-endpoint.openai.azure.com/"),
                new Azure.AzureKeyCredential("dummy-key-for-error-fallback"));
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error initializing OpenAI client: {Message}", ex.Message);
        
        // Use an empty endpoint that will fail gracefully rather than returning null
        return new Azure.AI.OpenAI.OpenAIClient(
            new Uri("https://dummy-endpoint.openai.azure.com/"),
            new Azure.AzureKeyCredential("dummy-key-for-exception-handler"));
    }
});

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure default files (must be before UseStaticFiles)
app.UseDefaultFiles();

// Use static files from the wwwroot folder
app.UseStaticFiles();

// Add cache control headers for HTML files to prevent browser caching
app.Use(async (context, next) => {
    if (context.Request.Path.Value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.Value == "/") {
        context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Append("Pragma", "no-cache");
        context.Response.Headers.Append("Expires", "0");
    }
    await next();
});

// Use CORS
app.UseCors("AllowAll");

// Use Entra ID Authentication
app.UseEntraIdAuthentication();

// Use session
app.UseSession();

// Map controllers
app.MapControllers();

app.Run();
