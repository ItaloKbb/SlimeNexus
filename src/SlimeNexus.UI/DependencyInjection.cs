using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.ViewModels;
using SlimeNexus.UI.Views;

namespace SlimeNexus.UI;

/// <summary>
/// Extension methods for configuring SlimeNexus UI services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds SlimeNexus UI services to the DI container.
    /// </summary>
    public static IServiceCollection AddSlimeNexusUI(this IServiceCollection services)
    {
        // ViewModels (Transient - new instance per request)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HardwareDashboardViewModel>();
        services.AddTransient<PetStatusViewModel>();
        services.AddTransient<TrayIconViewModel>();

        // Windows (Transient - new instance per request)
        services.AddTransient<MainWindow>();
        services.AddTransient<HardwareDashboardWindow>();
        services.AddTransient<PetStatusView>();

        // Tray Icon Window (Singleton - only one instance)
        services.AddSingleton<TrayIconWindow>();

        return services;
    }
}
