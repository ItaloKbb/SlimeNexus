using System.Threading;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SlimeNexus.Infrastructure;
using SlimeNexus.UI;
using SlimeNexus.UI.Services;
using Velopack;

// ╔═══════════════════════════════════════════════════════════════════════════╗
// ║ VELOPACK INITIALIZATION - Must be FIRST before any other code            ║
// ║ This handles install/uninstall/update hooks from the installer           ║
// ╚═══════════════════════════════════════════════════════════════════════════╝
VelopackApp.Build()
    .WithFirstRun((v) => 
    {
        // First run after install - create desktop shortcut, etc.
        System.Diagnostics.Debug.WriteLine($"SlimeNexus v{v} installed successfully!");
    })
    .WithAfterUpdateFastCallback((v) =>
    {
        // After Velopack applies an update, delete the installation flag
        // so the installer wizard runs again on next startup.
        try
        {
            var flagPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SlimeNexus", ".installed");
            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
                System.Diagnostics.Debug.WriteLine(
                    $"SlimeNexus updated to v{v} — installation flag cleared for re-setup.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear installation flag: {ex.Message}");
        }
    })
    .Run();

// Handle --reinstall flag: force the installer to run by deleting the flag
if (args.Contains("--reinstall", StringComparer.OrdinalIgnoreCase))
{
    try
    {
        var flagPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlimeNexus", ".installed");
        if (File.Exists(flagPath))
        {
            File.Delete(flagPath);
            System.Diagnostics.Debug.WriteLine("Installation flag cleared — reinstall mode active.");
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to clear installation flag for reinstall: {ex.Message}");
    }
}

// Single instance mutex
const string mutexName = "SlimeNexus_SingleInstance_Mutex";
using var mutex = new Mutex(true, mutexName, out var createdNew);

if (!createdNew)
{
    // Another instance is already running
    System.Diagnostics.Debug.WriteLine("SlimeNexus is already running.");
    try
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SlimeNexus");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] Mutex: another instance already running");
    }
    catch { }
    return 1;
}

try
{
    // Build the host with DI
    var host = CreateHostBuilder(args).Build();

    // Store host for global access
    App.Services = host.Services;

    // Start background services (API, etc.)
    await host.StartAsync();

    // Check for updates in background (non-blocking)
    _ = CheckForUpdatesAsync(host.Services.GetRequiredService<ILogger<UpdateManagerService>>());

    // Run Avalonia app (blocks until app exits)
    var exitCode = BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Graceful shutdown
    await host.StopAsync();

    return exitCode;
}
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Fatal error: {ex}");
    try
    {
        var crashLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlimeNexus", "crash.log");
        Directory.CreateDirectory(Path.GetDirectoryName(crashLog)!);
        File.WriteAllText(crashLog, $"[{DateTime.UtcNow:O}] Fatal error:\n{ex}");
    }
    catch { /* ignore logging errors */ }
    return 1;
}

static async Task CheckForUpdatesAsync(ILogger logger)
{
    try
    {
        var updateManager = App.Services?.GetService<UpdateManagerService>();
        if (updateManager is not null)
        {
            await updateManager.CheckAndApplyUpdatesAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Background update check failed");
    }
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddDebug();
#if DEBUG
            logging.SetMinimumLevel(LogLevel.Debug);
#else
            logging.SetMinimumLevel(LogLevel.Information);
#endif
        })
        .ConfigureServices((context, services) =>
        {
            // Add Infrastructure services (Hardware, Ollama, OpenClaw)
            services.AddSlimeNexusInfrastructure(options =>
            {
                // Configure Ollama
                options.OllamaOptions = new()
                {
                    BaseUrl = "http://localhost:11434",
                    DefaultModel = "llama3:8b-instruct-q4_K_M",
                    TimeoutSeconds = 300
                };
                
                // Configure OpenClaw (disabled gateway for local dev)
                options.OpenClawOptions = new()
                {
                    UseGateway = false,
                    DefaultTimeoutSeconds = 120
                };
            });
            
            // Add UI services
            services.AddSlimeNexusUI();

            // Add auto-updater service
            services.AddSingleton<UpdateManagerService>();

            // Add API as background service
            services.AddHostedService<ApiHostService>();
        });

static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
