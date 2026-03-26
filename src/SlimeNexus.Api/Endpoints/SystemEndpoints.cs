using Microsoft.AspNetCore.Mvc;
using SlimeNexus.Api.Services;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.Api.Endpoints;

/// <summary>
/// System information and status endpoints.
/// </summary>
public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system")
            .WithTags("System");

        group.MapGet("/info", GetSystemInfo)
            .WithName("GetSystemInfo")
            .WithSummary("Gets system information including hardware and AI status");

        group.MapGet("/hardware", GetHardwareInfo)
            .WithName("GetHardwareInfo")
            .WithSummary("Gets detailed hardware specifications");

        group.MapGet("/ai/status", GetAiStatus)
            .WithName("GetAiStatus")
            .WithSummary("Gets AI service status and available models");

        group.MapGet("/queue/stats", GetQueueStats)
            .WithName("GetQueueStats")
            .WithSummary("Gets task queue statistics");

        return app;
    }

    private static async Task<IResult> GetSystemInfo(
        [FromServices] IHardwareProber hardwareProber,
        [FromServices] IAiOrchestrator orchestrator,
        [FromServices] TaskQueue taskQueue,
        CancellationToken cancellationToken)
    {
        var specs = await hardwareProber.GetSpecsAsync(cancellationToken);
        var isReady = await orchestrator.IsReadyAsync(cancellationToken);

        return Results.Ok(new
        {
            version = "1.0.0",
            status = isReady ? "ready" : "degraded",
            hardware = new
            {
                gpu = specs.GpuName,
                vram_total_mb = specs.VramTotalMb,
                vram_available_mb = specs.VramAvailableMb,
                cuda = specs.SupportsCuda,
                cpu = specs.CpuName,
                cpu_cores = specs.CpuCoreCount,
                ram_total_mb = specs.RamTotalMb,
                can_run_local_inference = specs.CanRunLocalInference,
                suggested_quantization = specs.SuggestedQuantization
            },
            ai = new
            {
                ready = isReady,
                model = orchestrator.CurrentModelName,
                recommended_model = orchestrator.GetRecommendedModel(specs)
            },
            queue = new
            {
                pending = taskQueue.QueuedCount
            }
        });
    }

    private static async Task<IResult> GetHardwareInfo(
        [FromServices] IHardwareProber hardwareProber,
        CancellationToken cancellationToken)
    {
        var specs = await hardwareProber.GetSpecsAsync(cancellationToken);

        return Results.Ok(new
        {
            gpu = new
            {
                name = specs.GpuName,
                vram_total_mb = specs.VramTotalMb,
                vram_available_mb = specs.VramAvailableMb,
                supports_cuda = specs.SupportsCuda
            },
            cpu = new
            {
                name = specs.CpuName,
                cores = specs.CpuCoreCount
            },
            memory = new
            {
                total_mb = specs.RamTotalMb,
                available_mb = specs.RamAvailableMb
            },
            inference = new
            {
                can_run_local = specs.CanRunLocalInference,
                suggested_quantization = specs.SuggestedQuantization
            }
        });
    }

    private static async Task<IResult> GetAiStatus(
        [FromServices] IAiProvider aiProvider,
        [FromServices] IAiOrchestrator orchestrator,
        [FromServices] IHardwareProber hardwareProber,
        CancellationToken cancellationToken)
    {
        var isAvailable = await aiProvider.IsAvailableAsync(cancellationToken);
        var specs = await hardwareProber.GetSpecsAsync(cancellationToken);

        return Results.Ok(new
        {
            available = isAvailable,
            provider = aiProvider.ProviderName,
            current_model = orchestrator.CurrentModelName,
            recommended_model = orchestrator.GetRecommendedModel(specs),
            hardware_suitable = specs.CanRunLocalInference
        });
    }

    private static IResult GetQueueStats([FromServices] TaskQueue taskQueue)
    {
        return Results.Ok(new
        {
            queued = taskQueue.QueuedCount,
            timestamp = DateTimeOffset.UtcNow
        });
    }
}
