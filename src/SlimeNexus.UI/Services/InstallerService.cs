using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.UI.Services;

/// <summary>
/// Service responsible for first-run installation tasks including:
/// - System requirements validation
/// - Ollama installation and model download
/// - Hardware benchmark
/// - Storage space verification
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InstallerService
{
    private readonly IHardwareProber _hardwareProber;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<InstallerService> _logger;
    private string? _ollamaPath;

    public const string AppName = "SlimeNexus";
    public const string AppVersion = "1.0.0";
    public const string AppDescription = "AI Desktop Companion - Your Personal Tamagotchi";
    public const string DefaultOllamaModel = "llama3:8b-instruct-q4_K_M";
    
    // Minimum requirements
    public const ulong MinRamMb = 8192;          // 8 GB
    public const ulong MinDiskSpaceMb = 20480;   // 20 GB for app + model
    public const ulong MinVramMb = 4096;         // 4 GB (for local AI inference)

    public InstallerService(
        IHardwareProber hardwareProber,
        IAiProvider aiProvider,
        ILogger<InstallerService> logger)
    {
        _hardwareProber = hardwareProber;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the installation steps for display in the UI.
    /// </summary>
    public IReadOnlyList<InstallationStep> GetInstallationSteps()
    {
        return
        [
            new InstallationStep("verify_requirements", "Verificando Requisitos do Sistema", "Analisando hardware e espaço em disco"),
            new InstallationStep("benchmark", "Executando Benchmark", "Avaliando performance da máquina"),
            new InstallationStep("install_ollama", "Instalando Ollama", "Configurando runtime de IA local"),
            new InstallationStep("download_model", "Baixando Modelo de IA", "Pode levar alguns minutos..."),
            new InstallationStep("configure", "Finalizando Configuração", "Aplicando configurações otimizadas"),
            new InstallationStep("complete", "Instalação Completa", "SlimeNexus está pronto!")
        ];
    }

    /// <summary>
    /// Verifies system requirements and returns detailed results.
    /// </summary>
    public async Task<SystemRequirementsResult> VerifySystemRequirementsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying system requirements...");

        var specs = await _hardwareProber.GetSpecsAsync(cancellationToken);
        var diskSpace = GetAvailableDiskSpaceMb();

        // Determine recommended model based on VRAM
        var recommendedModel = specs.VramTotalMb switch
        {
            >= 12288 => "llama3:8b-instruct-q8_0",      // 12GB+ VRAM
            >= 8192 => "llama3:8b-instruct-q4_K_M",     // 8GB+ VRAM
            >= 6144 => "llama3:8b-instruct-q3_K_M",     // 6GB+ VRAM
            >= 4096 => "phi3:mini",                      // 4GB+ VRAM
            _ => "gemma:2b"                              // Low VRAM
        };

        var result = new SystemRequirementsResult
        {
            // Hardware info
            CpuName = specs.CpuName,
            CpuCores = specs.CpuCoreCount,
            GpuName = specs.GpuName,
            TotalRamMb = specs.RamTotalMb,
            AvailableRamMb = specs.RamAvailableMb,
            TotalVramMb = specs.VramTotalMb,
            AvailableVramMb = specs.VramAvailableMb,
            AvailableDiskSpaceMb = diskSpace,
            SupportsCuda = specs.SupportsCuda,
            CanRunLocalInference = specs.CanRunLocalInference,

            // Requirements check
            MeetsRamRequirement = specs.RamTotalMb >= MinRamMb,
            MeetsDiskRequirement = diskSpace >= MinDiskSpaceMb,
            MeetsVramRequirement = specs.VramTotalMb >= MinVramMb,
            RecommendedModel = recommendedModel,
            SuggestedQuantization = specs.SuggestedQuantization
        };

        result.MeetsAllRequirements = result.MeetsRamRequirement && 
                                       result.MeetsDiskRequirement;

        _logger.LogInformation(
            "Requirements check complete. RAM: {Ram}GB/{MinRam}GB, Disk: {Disk}GB/{MinDisk}GB, VRAM: {Vram}MB",
            result.TotalRamMb / 1024, MinRamMb / 1024,
            result.AvailableDiskSpaceMb / 1024, MinDiskSpaceMb / 1024,
            result.TotalVramMb);

        return result;
    }

    /// <summary>
    /// Runs a quick hardware benchmark to assess AI inference capabilities.
    /// </summary>
    public async Task<BenchmarkResult> RunBenchmarkAsync(
        IProgress<BenchmarkProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting hardware benchmark...");
        var stopwatch = Stopwatch.StartNew();
        var result = new BenchmarkResult();

        // Step 1: CPU Benchmark (parallel computation)
        progress?.Report(new BenchmarkProgress("CPU", 0, "Testando CPU..."));
        result.CpuScore = await RunCpuBenchmarkAsync(cancellationToken);
        progress?.Report(new BenchmarkProgress("CPU", 100, $"CPU Score: {result.CpuScore:F0}"));

        // Step 2: Memory Benchmark (throughput test)
        progress?.Report(new BenchmarkProgress("Memory", 0, "Testando memória..."));
        result.MemoryScore = await RunMemoryBenchmarkAsync(cancellationToken);
        progress?.Report(new BenchmarkProgress("Memory", 100, $"Memory Score: {result.MemoryScore:F0}"));

        // Step 3: Disk Benchmark (I/O test)
        progress?.Report(new BenchmarkProgress("Disk", 0, "Testando disco..."));
        result.DiskScore = await RunDiskBenchmarkAsync(cancellationToken);
        progress?.Report(new BenchmarkProgress("Disk", 100, $"Disk Score: {result.DiskScore:F0}"));

        // Step 4: AI Inference Test (if Ollama is available)
        if (await _aiProvider.IsAvailableAsync(cancellationToken))
        {
            progress?.Report(new BenchmarkProgress("AI", 0, "Testando inferência de IA..."));
            result.AiInferenceScore = await RunAiBenchmarkAsync(cancellationToken);
            progress?.Report(new BenchmarkProgress("AI", 100, $"AI Score: {result.AiInferenceScore:F0}"));
        }

        stopwatch.Stop();
        result.TotalDurationMs = stopwatch.ElapsedMilliseconds;
        result.OverallScore = CalculateOverallScore(result);
        result.PerformanceTier = DeterminePerformanceTier(result.OverallScore);

        _logger.LogInformation(
            "Benchmark complete. Overall: {Score:F0}, Tier: {Tier}, Duration: {Duration}ms",
            result.OverallScore, result.PerformanceTier, result.TotalDurationMs);

        return result;
    }

    /// <summary>
    /// Checks if Ollama is installed on the system.
    /// </summary>
    public bool IsOllamaInstalled()
    {
        return ResolveOllamaPath() is not null;
    }

    /// <summary>
    /// Installs Ollama using winget.
    /// </summary>
    public async Task<bool> InstallOllamaAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsOllamaInstalled())
        {
            progress?.Report("Ollama já está instalado.");
            return true;
        }

        _logger.LogInformation("Installing Ollama via winget...");
        progress?.Report("Instalando Ollama via Windows Package Manager...");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "install Ollama.Ollama --accept-source-agreements --accept-package-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            // Read output asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0 || output.Contains("already installed", StringComparison.OrdinalIgnoreCase))
            {
                // Refresh PATH so the current process can find the newly installed ollama
                RefreshPathFromRegistry();
                _ollamaPath = null; // Reset cached path so it's re-resolved

                if (ResolveOllamaPath() is not null)
                {
                    progress?.Report("✓ Ollama instalado e configurado no PATH.");
                }
                else
                {
                    progress?.Report("✓ Ollama instalado (PATH será atualizado após reinício).");
                }

                return true;
            }

            _logger.LogError("Ollama installation failed: {Error}", error);
            progress?.Report($"✗ Falha na instalação: {error}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Ollama installation");
            progress?.Report($"✗ Erro: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Downloads the specified AI model using Ollama.
    /// </summary>
    public async Task<bool> DownloadModelAsync(
        string model,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading model: {Model}", model);

        // Ensure Ollama service is running
        try
        {
            await EnsureOllamaServiceRunningAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure Ollama service is running");
            progress?.Report(new ModelDownloadProgress(-1, $"✗ Falha ao iniciar serviço Ollama: {ex.Message}"));
            return false;
        }

        var ollamaPath = ResolveOllamaPath();
        if (ollamaPath is null)
        {
            var errorMsg = "Executável 'ollama' não encontrado no PATH nem em diretórios conhecidos de instalação. Tente reiniciar o computador após a instalação do Ollama.";
            _logger.LogError(errorMsg);
            progress?.Report(new ModelDownloadProgress(-1, $"✗ {errorMsg}"));
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = $"pull {model}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                var errorMsg = $"Não foi possível iniciar o processo '{ollamaPath}'.";
                _logger.LogError(errorMsg);
                progress?.Report(new ModelDownloadProgress(-1, $"✗ {errorMsg}"));
                return false;
            }

            // Read stderr in background (ollama pull outputs progress to stderr)
            var stderrTask = Task.Run(async () =>
            {
                var lines = new List<string>();
                while (!process.StandardError.EndOfStream)
                {
                    var line = await process.StandardError.ReadLineAsync(cancellationToken);
                    if (line is not null)
                    {
                        lines.Add(line);
                        ParseAndReportProgress(line, progress);
                    }
                }
                return lines;
            }, cancellationToken);

            // Parse progress from stdout
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is not null)
                {
                    ParseAndReportProgress(line, progress);
                }
            }

            await process.WaitForExitAsync(cancellationToken);
            var stderrLines = await stderrTask;

            if (process.ExitCode == 0)
            {
                progress?.Report(new ModelDownloadProgress(100, "Download completo!"));
                return true;
            }

            var errorOutput = string.Join(Environment.NewLine, stderrLines);
            _logger.LogError("Model download failed (exit code {ExitCode}): {Error}", process.ExitCode, errorOutput);
            progress?.Report(new ModelDownloadProgress(-1, $"✗ Falha no download (código {process.ExitCode}): {errorOutput}"));
            return false;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            var errorMsg = "Comando 'ollama' não encontrado. O Ollama pode não estar no PATH do sistema. Tente reiniciar o computador após a instalação do Ollama.";
            _logger.LogError(ex, errorMsg);
            progress?.Report(new ModelDownloadProgress(-1, $"✗ {errorMsg}"));
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Exception during model download");
            progress?.Report(new ModelDownloadProgress(-1, $"✗ Erro inesperado: {ex.Message}"));
            return false;
        }
    }

    /// <summary>
    /// Gets the list of available models that can be installed.
    /// </summary>
    public IReadOnlyList<OllamaModelInfo> GetAvailableModels()
    {
        return
        [
            new OllamaModelInfo("llama3:8b-instruct-q4_K_M", "Llama 3 8B (Recomendado)", 4700, MinVramMb: 6144),
            new OllamaModelInfo("llama3:8b-instruct-q3_K_M", "Llama 3 8B (Menor)", 3800, MinVramMb: 4096),
            new OllamaModelInfo("phi3:mini", "Phi-3 Mini (Leve)", 2400, MinVramMb: 3072),
            new OllamaModelInfo("gemma:2b", "Gemma 2B (Ultra Leve)", 1500, MinVramMb: 2048),
            new OllamaModelInfo("mistral:7b-instruct-q4_K_M", "Mistral 7B", 4100, MinVramMb: 6144),
            new OllamaModelInfo("codellama:7b-instruct", "CodeLlama 7B (Código)", 4100, MinVramMb: 6144)
        ];
    }

    #region Private Methods

    private ulong GetAvailableDiskSpaceMb()
    {
        try
        {
            var appPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var driveInfo = new DriveInfo(Path.GetPathRoot(appPath) ?? "C:");
            return (ulong)(driveInfo.AvailableFreeSpace / (1024 * 1024));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get disk space");
            return 0;
        }
    }

    private async Task<double> RunCpuBenchmarkAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Run parallel computation to stress CPU
        await Task.Run(() =>
        {
            Parallel.For(0, Environment.ProcessorCount * 2, i =>
            {
                double result = 0;
                for (int j = 0; j < 10_000_000; j++)
                {
                    result += Math.Sqrt(j) * Math.Sin(j);
                }
            });
        }, cancellationToken);

        stopwatch.Stop();
        
        // Score based on time (lower is better, normalize to 0-100)
        var score = Math.Max(0, 100 - (stopwatch.ElapsedMilliseconds / 50.0));
        return Math.Min(100, score * (Environment.ProcessorCount / 4.0));
    }

    private async Task<double> RunMemoryBenchmarkAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        await Task.Run(() =>
        {
            // Allocate and access large arrays to test memory bandwidth
            const int size = 50_000_000;
            var array = new double[size];
            
            for (int i = 0; i < size; i++)
            {
                array[i] = i * 1.5;
            }
            
            double sum = 0;
            for (int i = 0; i < size; i++)
            {
                sum += array[i];
            }
        }, cancellationToken);

        stopwatch.Stop();
        
        // Score based on throughput
        var gbPerSecond = (50_000_000.0 * sizeof(double) / 1_000_000_000.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
        return Math.Min(100, gbPerSecond * 10);
    }

    private async Task<double> RunDiskBenchmarkAsync(CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"slimenexus_benchmark_{Guid.NewGuid()}.tmp");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Write test
            var data = new byte[50 * 1024 * 1024]; // 50 MB
            new Random().NextBytes(data);
            
            await File.WriteAllBytesAsync(tempPath, data, cancellationToken);
            
            // Read test
            _ = await File.ReadAllBytesAsync(tempPath, cancellationToken);
            
            stopwatch.Stop();
            
            // Score based on MB/s
            var mbPerSecond = (100.0) / (stopwatch.ElapsedMilliseconds / 1000.0);
            return Math.Min(100, mbPerSecond / 5);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private async Task<double> RunAiBenchmarkAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Simple prompt to measure inference speed
            var response = await _aiProvider.GenerateAsync(
                "Count from 1 to 10, one number per line.",
                cancellationToken);
            
            stopwatch.Stop();
            
            // Score based on tokens per second (estimate)
            var estimatedTokens = response.Split(' ').Length;
            var tokensPerSecond = estimatedTokens / (stopwatch.ElapsedMilliseconds / 1000.0);
            
            return Math.Min(100, tokensPerSecond * 2);
        }
        catch
        {
            return 0;
        }
    }

    private static double CalculateOverallScore(BenchmarkResult result)
    {
        // Weighted average
        return (result.CpuScore * 0.3) +
               (result.MemoryScore * 0.2) +
               (result.DiskScore * 0.2) +
               (result.AiInferenceScore * 0.3);
    }

    private static string DeterminePerformanceTier(double score)
    {
        return score switch
        {
            >= 80 => "Excelente",
            >= 60 => "Bom",
            >= 40 => "Moderado",
            >= 20 => "Básico",
            _ => "Limitado"
        };
    }

    private async Task EnsureOllamaServiceRunningAsync(CancellationToken cancellationToken)
    {
        if (await _aiProvider.IsAvailableAsync(cancellationToken))
            return;

        _logger.LogInformation("Starting Ollama service...");

        var ollamaPath = ResolveOllamaPath()
            ?? throw new InvalidOperationException(
                "Executável 'ollama' não encontrado. Verifique se o Ollama está instalado.");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ollamaPath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);

            // Wait for service to be ready
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(1000, cancellationToken);
                if (await _aiProvider.IsAvailableAsync(cancellationToken))
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Ollama service");
        }
    }

    /// <summary>
    /// Resolves the full path to the ollama executable.
    /// Checks the current PATH first, then known installation directories.
    /// Caches the result for subsequent calls.
    /// </summary>
    private string? ResolveOllamaPath()
    {
        if (_ollamaPath is not null && File.Exists(_ollamaPath))
            return _ollamaPath;

        // Try running ollama from PATH
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(5000);
            if (process?.ExitCode == 0)
            {
                _ollamaPath = "ollama";
                return _ollamaPath;
            }
        }
        catch
        {
            // Not in PATH, try known locations
        }

        // Search known Ollama installation directories
        string[] knownPaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ollama", "ollama.exe"),
        ];

        foreach (var path in knownPaths)
        {
            if (File.Exists(path))
            {
                _logger.LogInformation("Found Ollama at: {Path}", path);
                _ollamaPath = path;
                return _ollamaPath;
            }
        }

        _logger.LogWarning("Ollama executable not found in PATH or known locations");
        return null;
    }

    /// <summary>
    /// Refreshes the process PATH environment variable from the system registry
    /// so newly installed programs become visible without restarting the app.
    /// </summary>
    private void RefreshPathFromRegistry()
    {
        try
        {
            var machinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var newPath = $"{userPath};{machinePath}";

            Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
            _logger.LogInformation("Process PATH refreshed from registry");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh PATH from registry");
        }
    }

    private static void ParseAndReportProgress(string line, IProgress<ModelDownloadProgress>? progress)
    {
        if (progress is null) return;

        // Parse Ollama pull output for progress percentage
        if (line.Contains('%'))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith('%') && int.TryParse(part.TrimEnd('%'), out var percent))
                {
                    progress.Report(new ModelDownloadProgress(percent, line));
                    return;
                }
            }
        }

        progress.Report(new ModelDownloadProgress(-1, line));
    }

    #endregion
}

#region DTOs

public record InstallationStep(string Id, string Title, string Description);

public record SystemRequirementsResult
{
    // Hardware Info
    public string CpuName { get; init; } = "Unknown";
    public int CpuCores { get; init; }
    public string GpuName { get; init; } = "Unknown";
    public ulong TotalRamMb { get; init; }
    public ulong AvailableRamMb { get; init; }
    public ulong TotalVramMb { get; init; }
    public ulong AvailableVramMb { get; init; }
    public ulong AvailableDiskSpaceMb { get; init; }
    public bool SupportsCuda { get; init; }
    public bool CanRunLocalInference { get; init; }

    // Requirements Check
    public bool MeetsRamRequirement { get; init; }
    public bool MeetsDiskRequirement { get; init; }
    public bool MeetsVramRequirement { get; init; }
    public bool MeetsAllRequirements { get; set; }
    public string RecommendedModel { get; init; } = "llama3:8b-instruct-q4_K_M";
    public string SuggestedQuantization { get; init; } = "Q4_K_M";
}

public record BenchmarkResult
{
    public double CpuScore { get; set; }
    public double MemoryScore { get; set; }
    public double DiskScore { get; set; }
    public double AiInferenceScore { get; set; }
    public double OverallScore { get; set; }
    public string PerformanceTier { get; set; } = "Unknown";
    public long TotalDurationMs { get; set; }
}

public record BenchmarkProgress(string Component, int Percent, string Message);

public record ModelDownloadProgress(int Percent, string Message);

public record OllamaModelInfo(
    string ModelId,
    string DisplayName,
    ulong SizeMb,
    ulong MinVramMb);

#endregion
