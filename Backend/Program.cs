var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
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

// Register application services
builder.Services.AddTransient<Backend.Services.DocumentProcessingService>();
builder.Services.AddSingleton<Backend.Services.DocumentChunkingService>();
builder.Services.AddSingleton<Backend.Services.DocumentSearchService>();
builder.Services.AddTransient<Backend.Services.ChatService>();
builder.Services.AddSingleton<Backend.Services.IDocumentPersistenceService, Backend.Services.DocumentPersistenceService>(); // Add our document persistence service
builder.Services.AddSingleton<Backend.Services.SemanticChunker>(); // Register our new SemanticChunker service

// Register new refactored services
builder.Services.AddTransient<Backend.Services.DocumentContextService>(); // New service for document context preparation
builder.Services.AddTransient<Backend.Services.ChatAnalysisService>(); // New service for chat message analysis
builder.Services.AddTransient<Backend.Services.PromptEngineeringService>(); // New service for prompt construction
builder.Services.AddScoped<Backend.Services.OpenAIService>();
builder.Services.AddScoped<Backend.Services.AzureFunctionService>();

// Add OpenAI Configuration
builder.Services.AddSingleton<Backend.Configuration.OpenAIConfiguration>();

// Add Azure OpenAI Client using our configuration adapter
builder.Services.AddSingleton<Azure.AI.OpenAI.OpenAIClient>(sp => 
{
    var openAIConfig = sp.GetRequiredService<Backend.Configuration.OpenAIConfiguration>();
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    try
    {
        if (!openAIConfig.IsConfigured)
        {
            logger.LogWarning("OpenAI configuration is missing or incomplete. Some features will not work.");
            return null;
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
            return null;
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize Azure OpenAI client: {Message}", ex.Message);
        return null;
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
        context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        context.Response.Headers.Add("Pragma", "no-cache");
        context.Response.Headers.Add("Expires", "0");
    }
    await next();
});

// Use CORS
app.UseCors("AllowAll");

// Use session
app.UseSession();

// Map controllers
app.MapControllers();

app.Run();
