using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace SlimeNexus.Api.Endpoints;

/// <summary>
/// Health check endpoints for monitoring.
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        }).WithTags("Health");

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        }).WithTags("Health");

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false // Always returns healthy
        }).WithTags("Health");

        return app;
    }

    private static async Task WriteHealthResponse(
        HttpContext context, 
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data,
                error = e.Value.Exception?.Message
            })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
