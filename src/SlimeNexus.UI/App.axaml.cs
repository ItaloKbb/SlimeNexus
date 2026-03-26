using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.Views;

namespace SlimeNexus.UI;

public partial class App : Application
{
    /// <summary>
    /// Global service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    public static T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>
    /// Tries to get a service from the DI container.
    /// </summary>
    public static T? TryGetService<T>() where T : class => Services.GetService<T>();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Set shutdown mode to explicit (app keeps running in background)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Create and show the mini tray window
            var trayWindow = Services.GetRequiredService<TrayIconWindow>();
            desktop.MainWindow = trayWindow;
            trayWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
