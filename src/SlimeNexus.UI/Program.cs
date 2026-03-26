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
        Console.WriteLine($"SlimeNexus v{v} installed successfully!");
    })
    .Run();

// Single instance mutex
const string mutexName = "SlimeNexus_SingleInstance_Mutex";
using var mutex = new Mutex(true, mutexName, out var createdNew);

if (!createdNew)
{
    // Another instance is already running
    Console.WriteLine("SlimeNexus is already running.");
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
    Console.Error.WriteLine($"Fatal error: {ex}");
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
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Debug);
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
