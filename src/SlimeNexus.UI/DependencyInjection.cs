using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.Services;
using SlimeNexus.UI.ViewModels;
using SlimeNexus.UI.ViewModels.Installer;
using SlimeNexus.UI.Views;
using SlimeNexus.UI.Views.Installer;

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
        // Services
        services.AddSingleton<InstallerService>();

        // ViewModels (Transient - new instance per request)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<HardwareDashboardViewModel>();
        services.AddTransient<PetStatusViewModel>();
        services.AddTransient<TrayIconViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<TasksViewModel>();

        // Installer ViewModels
        services.AddTransient<InstallerWizardViewModel>();

        // Windows (Transient - new instance per request)
        services.AddTransient<MainWindow>();
        services.AddTransient<HardwareDashboardWindow>();
        services.AddTransient<PetStatusView>();

        // Installer Window
        services.AddTransient<InstallerWindow>();

        // Tray Icon Window (Singleton - only one instance)
        services.AddSingleton<TrayIconWindow>();

        return services;
    }
}
