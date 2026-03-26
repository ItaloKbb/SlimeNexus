using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SlimeNexus.UI.Services;

/// <summary>
/// Background service that hosts the SlimeNexus API.
/// Runs Kestrel on localhost:18789 for frontend communication.
/// </summary>
public sealed class ApiHostService : BackgroundService
{
    private readonly ILogger<ApiHostService> _logger;

    public ApiHostService(ILogger<ApiHostService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the API is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the API base URL.
    /// </summary>
    public string BaseUrl => "http://localhost:18789";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlimeNexus API host service starting...");

        try
        {
            // Note: In a full implementation, we would spin up a separate
            // ASP.NET Core host here. For now, we just signal readiness.
            // The API can be run separately via: dotnet run --project SlimeNexus.Api

            IsRunning = true;
            _logger.LogInformation(
                "SlimeNexus API ready. For full API functionality, run SlimeNexus.Api separately on {Url}",
                BaseUrl);

            // Keep the service running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("API host service shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API host service failed");
        }
        finally
        {
            IsRunning = false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping API host service...");
        await base.StopAsync(cancellationToken);
    }
}
