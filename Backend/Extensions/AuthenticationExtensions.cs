using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Backend.Configuration;
using Backend.Services;
using Backend.Services.Interfaces;

namespace Backend.Extensions;

/// <summary>
/// Extension methods for configuring Entra ID authentication
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Configures Entra ID (Azure AD) JWT Bearer authentication
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddEntraIdAuthentication(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register Entra ID configuration
        var entraIdConfig = new EntraIdConfiguration();
        configuration.GetSection(EntraIdConfiguration.SectionName).Bind(entraIdConfig);
        services.AddSingleton(entraIdConfig);

        // Register authentication service
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        // Only configure authentication if Entra ID is properly configured
        if (!entraIdConfig.IsConfigured)
        {
            var logger = services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Entra ID configuration is missing. Authentication will be disabled.");
            return services;
        }

        // Configure JWT Bearer authentication with Microsoft Identity Web
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration, EntraIdConfiguration.SectionName);

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Removed fallback policy to allow anonymous access to endpoints without [Authorize]
            // options.FallbackPolicy = options.DefaultPolicy;
            
            // Custom policy for API access
            options.AddPolicy("ApiAccess", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scp", "access_as_user");
            });
        });

        return services;
    }

    /// <summary>
    /// Configures authentication middleware in the request pipeline
    /// </summary>
    /// <param name="app">Web application</param>
    /// <returns>Web application for chaining</returns>
    public static WebApplication UseEntraIdAuthentication(this WebApplication app)
    {
        // Add authentication middleware
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
