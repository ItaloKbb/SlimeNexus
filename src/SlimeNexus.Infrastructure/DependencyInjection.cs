using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Infrastructure.AI;
using SlimeNexus.Infrastructure.Executors;
using SlimeNexus.Infrastructure.Hardware;

namespace SlimeNexus.Infrastructure;

/// <summary>
/// Extension methods for configuring SlimeNexus.Infrastructure services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all SlimeNexus Infrastructure services to the DI container.
    /// </summary>
    public static IServiceCollection AddSlimeNexusInfrastructure(
        this IServiceCollection services,
        Action<InfrastructureOptions>? configure = null)
    {
        var options = new InfrastructureOptions();
        configure?.Invoke(options);

        // Hardware probing
        services.AddSingleton<IHardwareProber, WindowsHardwareProber>();

        // Ollama AI client
        services.AddSingleton(options.OllamaOptions);
        services.AddHttpClient<OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(options.OllamaOptions.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.OllamaOptions.TimeoutSeconds);
        });
        services.AddSingleton<IAiProvider>(sp => sp.GetRequiredService<OllamaClient>());

        // OpenClaw executor
        services.AddSingleton(options.OpenClawOptions);
        services.AddHttpClient<OpenClawExecutor>(client =>
        {
            client.BaseAddress = new Uri(options.OpenClawOptions.GatewayUrl);
            client.Timeout = TimeSpan.FromSeconds(options.OpenClawOptions.DefaultTimeoutSeconds);
        });
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITaskExecutor, OpenClawExecutor>());

        // Task executor registry
        services.AddSingleton<ITaskExecutorRegistry>(sp =>
        {
            var executors = sp.GetServices<ITaskExecutor>();
            return new TaskExecutorRegistry(executors);
        });

        return services;
    }

    /// <summary>
    /// Adds only the Ollama client to the DI container.
    /// </summary>
    public static IServiceCollection AddOllamaClient(
        this IServiceCollection services,
        Action<OllamaOptions>? configure = null)
    {
        var options = new OllamaOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHttpClient<OllamaClient>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        services.AddSingleton<IAiProvider>(sp => sp.GetRequiredService<OllamaClient>());

        return services;
    }

    /// <summary>
    /// Adds only the hardware probing services to the DI container.
    /// </summary>
    public static IServiceCollection AddHardwareProbing(this IServiceCollection services)
    {
        services.AddSingleton<IHardwareProber, WindowsHardwareProber>();
        return services;
    }
}

/// <summary>
/// Configuration options for SlimeNexus Infrastructure.
/// </summary>
public sealed class InfrastructureOptions
{
    /// <summary>Ollama client configuration.</summary>
    public OllamaOptions OllamaOptions { get; set; } = new();

    /// <summary>OpenClaw executor configuration.</summary>
    public OpenClawOptions OpenClawOptions { get; set; } = new();
}
