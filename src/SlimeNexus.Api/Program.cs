using SlimeNexus.Api;
using SlimeNexus.Api.Endpoints;
using SlimeNexus.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on local port 18789
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(18789);
});

// Add services
builder.Services.AddSlimeNexusApi(builder.Configuration);
builder.Services.AddSlimeNexusInfrastructure(options =>
{
    // Configure from appsettings.json or use defaults
    var ollamaSection = builder.Configuration.GetSection("Ollama");
    if (ollamaSection.Exists())
    {
        options.OllamaOptions = ollamaSection.Get<SlimeNexus.Infrastructure.AI.OllamaOptions>() 
            ?? new();
    }

    var openClawSection = builder.Configuration.GetSection("OpenClaw");
    if (openClawSection.Exists())
    {
        options.OpenClawOptions = openClawSection.Get<SlimeNexus.Infrastructure.Executors.OpenClawOptions>() 
            ?? new();
    }
});

var app = builder.Build();

// Configure middleware pipeline
app.UseSlimeNexusMiddleware();

// Map endpoints
app.MapTaskEndpoints();
app.MapHealthEndpoints();
app.MapSystemEndpoints();

// Startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("SlimeNexus API starting on http://localhost:18789");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
