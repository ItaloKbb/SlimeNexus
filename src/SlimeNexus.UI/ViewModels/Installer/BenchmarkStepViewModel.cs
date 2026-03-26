using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Benchmark step - runs performance tests.
/// </summary>
public sealed partial class BenchmarkStepViewModel : InstallerStepViewModelBase
{
    private readonly InstallerService _installerService;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _benchmarkComplete;

    [ObservableProperty]
    private int _overallProgress;

    // Individual scores
    [ObservableProperty]
    private double _cpuScore;

    [ObservableProperty]
    private int _cpuProgress;

    [ObservableProperty]
    private string _cpuStatus = "Aguardando...";

    [ObservableProperty]
    private double _memoryScore;

    [ObservableProperty]
    private int _memoryProgress;

    [ObservableProperty]
    private string _memoryStatus = "Aguardando...";

    [ObservableProperty]
    private double _diskScore;

    [ObservableProperty]
    private int _diskProgress;

    [ObservableProperty]
    private string _diskStatus = "Aguardando...";

    [ObservableProperty]
    private double _aiScore;

    [ObservableProperty]
    private int _aiProgress;

    [ObservableProperty]
    private string _aiStatus = "Aguardando...";

    // Overall results
    [ObservableProperty]
    private double _overallScore;

    [ObservableProperty]
    private string _performanceTier = "—";

    [ObservableProperty]
    private string _performanceTierColor = "Gray";

    [ObservableProperty]
    private string _performanceDescription = string.Empty;

    [ObservableProperty]
    private long _totalDurationMs;

    public BenchmarkStepViewModel(
        InstallerWizardViewModel wizard,
        InstallerService installerService) : base(wizard)
    {
        _installerService = installerService;
        Title = "Benchmark de Performance";
        Description = "Avaliando a capacidade do seu sistema para IA local";
        Icon = "⚡";
        CanGoNext = false;
    }

    public override async Task OnEnteringAsync()
    {
        if (!BenchmarkComplete)
        {
            await RunBenchmarkAsync();
        }
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        IsRunning = true;
        IsBusy = true;
        CanGoNext = false;
        CanGoBack = false;
        OverallProgress = 0;

        var progress = new Progress<BenchmarkProgress>(p =>
        {
            switch (p.Component)
            {
                case "CPU":
                    CpuProgress = p.Percent;
                    CpuStatus = p.Message;
                    OverallProgress = p.Percent / 4;
                    break;
                case "Memory":
                    MemoryProgress = p.Percent;
                    MemoryStatus = p.Message;
                    OverallProgress = 25 + (p.Percent / 4);
                    break;
                case "Disk":
                    DiskProgress = p.Percent;
                    DiskStatus = p.Message;
                    OverallProgress = 50 + (p.Percent / 4);
                    break;
                case "AI":
                    AiProgress = p.Percent;
                    AiStatus = p.Message;
                    OverallProgress = 75 + (p.Percent / 4);
                    break;
            }
        });

        try
        {
            var result = await _installerService.RunBenchmarkAsync(progress);

            // Update individual scores
            CpuScore = result.CpuScore;
            CpuStatus = GetScoreRating(result.CpuScore);
            CpuProgress = 100;

            MemoryScore = result.MemoryScore;
            MemoryStatus = GetScoreRating(result.MemoryScore);
            MemoryProgress = 100;

            DiskScore = result.DiskScore;
            DiskStatus = GetScoreRating(result.DiskScore);
            DiskProgress = 100;

            AiScore = result.AiInferenceScore;
            AiStatus = result.AiInferenceScore > 0 
                ? GetScoreRating(result.AiInferenceScore) 
                : "Ollama não instalado";
            AiProgress = 100;

            // Overall results
            OverallScore = result.OverallScore;
            PerformanceTier = result.PerformanceTier;
            TotalDurationMs = result.TotalDurationMs;
            OverallProgress = 100;

            // Color and description based on tier
            (PerformanceTierColor, PerformanceDescription) = result.PerformanceTier switch
            {
                "Ultra" => ("Purple", "🚀 Performance excepcional! IA local funcionará perfeitamente."),
                "High" => ("Green", "💪 Ótima performance! Modelos grandes rodarão bem."),
                "Medium" => ("DodgerBlue", "👍 Boa performance. Modelos médios são recomendados."),
                "Low" => ("Orange", "⚠️ Performance limitada. Use modelos menores."),
                _ => ("Red", "❌ Performance muito baixa. Experiência pode ser lenta.")
            };

            // Store results
            Wizard.SetBenchmarkResults(result);

            BenchmarkComplete = true;
            CanGoNext = true;
            CanGoBack = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro no benchmark: {ex.Message}";
            CanGoBack = true;
        }
        finally
        {
            IsRunning = false;
            IsBusy = false;
            UpdateWizardNavigation();
        }
    }

    private static string GetScoreRating(double score) => score switch
    {
        >= 90 => "Excelente",
        >= 70 => "Muito Bom",
        >= 50 => "Bom",
        >= 30 => "Razoável",
        _ => "Baixo"
    };
}
