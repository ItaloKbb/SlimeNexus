using System.Reflection;
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

    private TrayIconWindow? _trayWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        try
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
        catch (Exception ex)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlimeNexus");
                Directory.CreateDirectory(logDir);
                File.WriteAllText(Path.Combine(logDir, "crash.log"),
                    $"[{DateTime.UtcNow:O}] OnFrameworkInitializationCompleted error:\n{ex}");
            }
            catch { }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2)
            {
                desktop2.Shutdown(1);
            }
        }
    }

    private void ShowMainApp(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _trayWindow = Services.GetRequiredService<TrayIconWindow>();
        desktop.MainWindow = _trayWindow;

        // Don't show the window — it starts hidden in the tray
        SetupTrayIconEvents(desktop);
    }

    private void SetupTrayIconEvents(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var trayIcons = TrayIcon.GetIcons(this);
        if (trayIcons is null || trayIcons.Count == 0) return;

        var trayIcon = trayIcons[0];
        trayIcon.Clicked += (_, _) => ToggleTrayWindow();

        if (trayIcon.Menu is not { } menu) return;

        foreach (var item in menu.Items)
        {
            if (item is NativeMenuItem menuItem)
            {
                switch (menuItem.Header)
                {
                    case "Abrir Painel":
                        menuItem.Click += (_, _) => ShowTrayWindow();
                        break;
                    case "Dashboard":
                        menuItem.Click += (_, _) =>
                        {
                            var mainWindow = Services.GetRequiredService<MainWindow>();
                            mainWindow.Show();
                            mainWindow.Activate();
                        };
                        break;
                    case "Info de Hardware":
                        menuItem.Click += (_, _) =>
                        {
                            var hwWindow = Services.GetRequiredService<HardwareDashboardWindow>();
                            hwWindow.Show();
                            hwWindow.Activate();
                        };
                        break;
                    case "Sair":
                        menuItem.Click += (_, _) =>
                        {
                            if (_trayWindow is not null)
                                _trayWindow.ForceClose = true;
                            desktop.Shutdown();
                        };
                        break;
                }
            }
        }
    }

    private void ToggleTrayWindow()
    {
        if (_trayWindow is null) return;

        if (_trayWindow.IsVisible)
        {
            _trayWindow.Hide();
        }
        else
        {
            ShowTrayWindow();
        }
    }

    private void ShowTrayWindow()
    {
        if (_trayWindow is null) return;

        _trayWindow.Show();
        _trayWindow.Activate();

        if (_trayWindow.WindowState == WindowState.Minimized)
            _trayWindow.WindowState = WindowState.Normal;
    }

    private static bool ShouldShowInstaller()
    {
        var configPath = GetInstallationFlagPath();

        // If the flag file doesn't exist, this is a first install
        if (!File.Exists(configPath))
            return true;

        // If the flag file exists, check if it matches the current version.
        // When a new version is installed (via Velopack or manually), the installer
        // should re-run to validate/update the environment.
        try
        {
            var storedContent = File.ReadAllText(configPath).Trim();
            var currentVersion = GetCurrentAppVersion();

            // Legacy format: file contains only a date (no version).
            // Force re-install to migrate to versioned flag.
            if (!storedContent.Contains('|'))
                return true;

            var storedVersion = storedContent.Split('|')[0];
            return !string.Equals(storedVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If we can't read the file, assume installer is needed
            return true;
        }
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
            // Store version + timestamp so we can detect version changes
            var version = GetCurrentAppVersion();
            File.WriteAllText(configPath, $"{version}|{DateTime.UtcNow:O}");
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

    /// <summary>
    /// Gets the current application version from the assembly.
    /// </summary>
    private static string GetCurrentAppVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? InstallerService.AppVersion;
    }

    /// <summary>
    /// Resets the installation flag, forcing the installer wizard to show on next startup.
    /// Can be called from settings/UI to allow a full reinstall.
    /// </summary>
    public static bool ResetInstallation()
    {
        try
        {
            var flagPath = GetInstallationFlagPath();
            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
