using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SlimeNexus.UI.Services;
using SlimeNexus.UI.Views;
using SlimeNexus.UI.Views.Installer;

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

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Set shutdown mode to explicit (app keeps running in background)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Check if this is first run and show installer wizard
            if (ShouldShowInstaller())
            {
                var installerWindow = Services.GetRequiredService<InstallerWindow>();
                desktop.MainWindow = installerWindow;
                installerWindow.Show();

                // Wait for installer to complete
                installerWindow.Closed += (_, _) =>
                {
                    if (installerWindow.InstallationResult == true)
                    {
                        // Installation successful, mark as complete and show main app
                        MarkInstallationComplete();
                        ShowMainApp(desktop);
                    }
                    else
                    {
                        // Installation cancelled, exit app
                        desktop.Shutdown(1);
                    }
                };
            }
            else
            {
                // Normal startup - show tray icon
                ShowMainApp(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowMainApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var trayWindow = Services.GetRequiredService<TrayIconWindow>();
        desktop.MainWindow = trayWindow;
        trayWindow.Show();
    }

    private static bool ShouldShowInstaller()
    {
        // Check if installation has been completed before
        var configPath = GetInstallationFlagPath();
        return !File.Exists(configPath);
    }

    private static void MarkInstallationComplete()
    {
        try
        {
            var configPath = GetInstallationFlagPath();
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(configPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Ignore errors - app can still run
        }
    }

    private static string GetInstallationFlagPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "SlimeNexus", ".installed");
    }
}
