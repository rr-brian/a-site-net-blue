using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RaiToolbox.Services;

var builder = WebApplication.CreateBuilder(args);

// Add authentication services
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraId"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

// Add Razor pages with Microsoft Identity UI
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// Add services
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
builder.Services.AddSingleton<IConversationLogger, ConversationLogger>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddSingleton<IDocumentService, DocumentService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add controllers for API endpoints
builder.Services.AddControllers();

// Add HttpClient for external API calls
builder.Services.AddHttpClient();

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapRazorPages();
app.MapControllers();

app.Run();