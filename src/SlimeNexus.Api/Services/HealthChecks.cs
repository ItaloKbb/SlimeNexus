using Microsoft.Extensions.Diagnostics.HealthChecks;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Api.Services;

/// <summary>
/// Health check for Ollama AI service availability.
/// </summary>
public sealed class OllamaHealthCheck : IHealthCheck
{
    private readonly IAiProvider _aiProvider;

    public OllamaHealthCheck(IAiProvider aiProvider)
    {
        _aiProvider = aiProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _aiProvider.IsAvailableAsync(cancellationToken);
            
            return isAvailable
                ? HealthCheckResult.Healthy($"Ollama is available ({_aiProvider.ProviderName})")
                : HealthCheckResult.Unhealthy("Ollama is not responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama check failed", ex);
        }
    }
}

/// <summary>
/// Health check for hardware monitoring.
/// </summary>
public sealed class HardwareHealthCheck : IHealthCheck
{
    private readonly IHardwareProber _hardwareProber;

    public HardwareHealthCheck(IHardwareProber hardwareProber)
    {
        _hardwareProber = hardwareProber;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var specs = await _hardwareProber.GetSpecsAsync(cancellationToken);
            
            var data = new Dictionary<string, object>
            {
                ["gpu"] = specs.GpuName,
                ["vram_mb"] = specs.VramTotalMb,
                ["cuda"] = specs.SupportsCuda,
                ["can_run_local"] = specs.CanRunLocalInference
            };

            return specs.CanRunLocalInference
                ? HealthCheckResult.Healthy("Hardware meets requirements", data)
                : HealthCheckResult.Degraded("Hardware may not support local AI", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hardware probe failed", ex);
        }
    }
}
