using System.Text.Json;
using System.Text.Json.Serialization;
using SlimeNexus.Api.Services;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Api;

/// <summary>
/// Extension methods for configuring SlimeNexus API services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds SlimeNexus API services to the DI container.
    /// </summary>
    public static IServiceCollection AddSlimeNexusApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure JSON serialization
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Configure CORS for frontend communication
        services.AddCors(options =>
        {
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? ["http://localhost:3000", "http://localhost:5173"];

            options.AddPolicy("SlimeNexusFrontend", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });

            // Development policy (more permissive)
            options.AddPolicy("Development", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Add Problem Details for error responses
        services.AddProblemDetails();

        // Add API-specific services
        services.AddSingleton<IAiOrchestrator, AiOrchestrator>();
        services.AddSingleton<TaskQueue>();
        services.AddHostedService<TaskProcessorService>();

        // Add health checks
        services.AddHealthChecks()
            .AddCheck<OllamaHealthCheck>("ollama")
            .AddCheck<HardwareHealthCheck>("hardware");

        return services;
    }

    /// <summary>
    /// Configures the SlimeNexus API middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseSlimeNexusMiddleware(this WebApplication app)
    {
        // Use appropriate CORS policy based on environment
        var corsPolicy = app.Environment.IsDevelopment() 
            ? "Development" 
            : "SlimeNexusFrontend";
        
        app.UseCors(corsPolicy);

        // Global exception handling
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Request logging in development
        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("{Method} {Path}", context.Request.Method, context.Request.Path);
                await next();
            });
        }

        return app;
    }
}
