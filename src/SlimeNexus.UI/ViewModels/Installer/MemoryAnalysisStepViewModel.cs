using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Memory analysis step - shows detailed RAM and VRAM information.
/// </summary>
public sealed partial class MemoryAnalysisStepViewModel : InstallerStepViewModelBase
{
    // RAM Analysis
    [ObservableProperty]
    private double _totalRamGb;

    [ObservableProperty]
    private double _availableRamGb;

    [ObservableProperty]
    private double _usedRamGb;

    [ObservableProperty]
    private int _ramUsagePercent;

    [ObservableProperty]
    private string _ramStatus = string.Empty;

    [ObservableProperty]
    private string _ramStatusColor = "Gray";

    // VRAM Analysis
    [ObservableProperty]
    private double _totalVramGb;

    [ObservableProperty]
    private double _availableVramGb;

    [ObservableProperty]
    private bool _hasDiscreteGpu;

    [ObservableProperty]
    private string _vramStatus = string.Empty;

    [ObservableProperty]
    private string _vramStatusColor = "Gray";

    // Recommendations
    [ObservableProperty]
    private string _ramRecommendation = string.Empty;

    [ObservableProperty]
    private string _vramRecommendation = string.Empty;

    [ObservableProperty]
    private string _overallMemoryStatus = string.Empty;

    [ObservableProperty]
    private bool _canRunLocalAi;

    [ObservableProperty]
    private bool _supportsCuda;

    [ObservableProperty]
    private string _suggestedQuantization = string.Empty;

    public MemoryAnalysisStepViewModel(InstallerWizardViewModel wizard) : base(wizard)
    {
        Title = "Análise de Memória";
        Description = "Verificando RAM e VRAM para inferência de IA";
        Icon = "🧠";
    }

    public override Task OnEnteringAsync()
    {
        LoadMemoryAnalysis();
        return Task.CompletedTask;
    }

    private void LoadMemoryAnalysis()
    {
        var sysReq = Wizard.SystemRequirements;
        if (sysReq is null)
        {
            StatusMessage = "Erro: requisitos do sistema não verificados";
            return;
        }

        // RAM Analysis
        TotalRamGb = sysReq.TotalRamMb / 1024.0;
        AvailableRamGb = sysReq.AvailableRamMb / 1024.0;
        UsedRamGb = TotalRamGb - AvailableRamGb;
        RamUsagePercent = TotalRamGb > 0 ? (int)((UsedRamGb / TotalRamGb) * 100) : 0;

        (RamStatus, RamStatusColor) = TotalRamGb switch
        {
            >= 32 => ("Excelente", "Green"),
            >= 16 => ("Muito Bom", "LimeGreen"),
            >= 8 => ("Suficiente", "Orange"),
            _ => ("Insuficiente", "Red")
        };

        RamRecommendation = TotalRamGb switch
        {
            >= 32 => "✨ RAM abundante! Pode rodar modelos grandes e múltiplas tarefas.",
            >= 16 => "👍 RAM adequada para a maioria dos modelos de IA.",
            >= 8 => "⚠️ RAM limitada. Feche outros programas durante uso de IA.",
            _ => "❌ RAM insuficiente. Upgrade recomendado para melhor experiência."
        };

        // VRAM Analysis
        TotalVramGb = sysReq.TotalVramMb / 1024.0;
        AvailableVramGb = sysReq.AvailableVramMb / 1024.0;
        HasDiscreteGpu = sysReq.TotalVramMb > 0;
        SupportsCuda = sysReq.SupportsCuda;
        CanRunLocalAi = sysReq.CanRunLocalInference;
        SuggestedQuantization = sysReq.SuggestedQuantization;

        if (!HasDiscreteGpu)
        {
            VramStatus = "Não detectada";
            VramStatusColor = "Gray";
            VramRecommendation = "🖥️ GPU integrada detectada. IA rodará via CPU (mais lento mas funcional).";
        }
        else
        {
            (VramStatus, VramStatusColor) = TotalVramGb switch
            {
                >= 12 => ("Excelente", "Green"),
                >= 8 => ("Muito Bom", "LimeGreen"),
                >= 6 => ("Bom", "DodgerBlue"),
                >= 4 => ("Razoável", "Orange"),
                _ => ("Limitada", "Red")
            };

            VramRecommendation = TotalVramGb switch
            {
                >= 12 => "🚀 VRAM excelente! Modelos grandes como Llama 3 8B Q8 rodam perfeitamente.",
                >= 8 => "💪 VRAM ótima! Llama 3 8B Q4 é recomendado.",
                >= 6 => "👍 VRAM adequada. Llama 3 8B Q3 ou Mistral 7B funcionam bem.",
                >= 4 => "⚠️ VRAM limitada. Recomendamos Phi-3 Mini ou Gemma 2B.",
                _ => "❌ VRAM muito baixa. Modelos rodarão via CPU."
            };
        }

        // Overall status
        OverallMemoryStatus = (TotalRamGb, TotalVramGb) switch
        {
            ( >= 16, >= 8) => "🎮 Sistema ideal para IA local com performance máxima!",
            ( >= 16, >= 4) => "💻 Sistema bom para IA local com modelos médios.",
            ( >= 8, >= 4) => "🔧 Sistema funcional. Modelos menores são recomendados.",
            ( >= 8, _) => "⚙️ IA rodará via CPU. Performance mais lenta mas estável.",
            _ => "⚠️ Sistema com limitações. Experiência pode não ser ideal."
        };
    }

    // Memory bar visualization helpers
    public double RamBarWidth => Math.Min(100, (TotalRamGb / 64.0) * 100);
    public double RamUsedWidth => Math.Min(100, (UsedRamGb / 64.0) * 100);
    public double VramBarWidth => HasDiscreteGpu ? Math.Min(100, (TotalVramGb / 24.0) * 100) : 0;
}
