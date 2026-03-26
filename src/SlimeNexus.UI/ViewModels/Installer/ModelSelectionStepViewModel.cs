using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Model selection step - allows user to choose the AI model.
/// </summary>
public sealed partial class ModelSelectionStepViewModel : InstallerStepViewModelBase
{
    private readonly InstallerService _installerService;

    [ObservableProperty]
    private InstallerModelOption? _selectedModel;

    [ObservableProperty]
    private string _recommendedModelName = string.Empty;

    [ObservableProperty]
    private string _selectionInfo = string.Empty;

    public ObservableCollection<InstallerModelOption> AvailableModels { get; } = [];

    public ModelSelectionStepViewModel(
        InstallerWizardViewModel wizard,
        InstallerService installerService) : base(wizard)
    {
        _installerService = installerService;
        Title = "Escolha do Modelo de IA";
        Description = "Selecione o modelo que melhor se adapta ao seu hardware";
        Icon = "🤖";
    }

    public override Task OnEnteringAsync()
    {
        LoadAvailableModels();
        return Task.CompletedTask;
    }

    public override Task OnLeavingAsync()
    {
        if (SelectedModel is not null)
        {
            Wizard.SelectedModel = SelectedModel.ModelId;
        }
        return Task.CompletedTask;
    }

    private void LoadAvailableModels()
    {
        AvailableModels.Clear();

        var sysReq = Wizard.SystemRequirements;
        var vramMb = sysReq?.TotalVramMb ?? 0;
        var recommended = sysReq?.RecommendedModel ?? InstallerService.DefaultOllamaModel;
        RecommendedModelName = recommended;

        var models = _installerService.GetAvailableModels();

        foreach (var model in models)
        {
            var canRun = vramMb >= model.MinVramMb || vramMb == 0; // 0 = CPU only mode
            var isRecommended = model.ModelId == recommended;

            var vm = new InstallerModelOption
            {
                ModelId = model.ModelId,
                DisplayName = model.DisplayName,
                SizeMb = model.SizeMb,
                MinVramMb = model.MinVramMb,
                CanRun = canRun,
                IsRecommended = isRecommended,
                Description = GetModelDescription(model.ModelId),
                PerformanceNote = GetPerformanceNote(model, vramMb)
            };

            AvailableModels.Add(vm);

            // Auto-select recommended model
            if (isRecommended)
            {
                SelectedModel = vm;
            }
        }

        // If no model was recommended, select the first runnable one
        SelectedModel ??= AvailableModels.FirstOrDefault(m => m.CanRun);

        UpdateSelectionInfo();
    }

    partial void OnSelectedModelChanged(InstallerModelOption? value)
    {
        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        if (SelectedModel is null)
        {
            SelectionInfo = "Selecione um modelo para continuar.";
            CanGoNext = false;
        }
        else if (!SelectedModel.CanRun)
        {
            SelectionInfo = "⚠️ Este modelo pode não rodar bem no seu hardware.";
            CanGoNext = true; // Allow anyway with warning
        }
        else if (SelectedModel.IsRecommended)
        {
            SelectionInfo = "✅ Modelo recomendado para seu hardware!";
            CanGoNext = true;
        }
        else
        {
            SelectionInfo = "👍 Modelo compatível com seu sistema.";
            CanGoNext = true;
        }

        UpdateWizardNavigation();
    }

    private static string GetModelDescription(string modelId) => modelId switch
    {
        "llama3:8b-instruct-q4_K_M" => "Modelo versátil da Meta. Excelente equilíbrio entre qualidade e performance.",
        "llama3:8b-instruct-q8_0" => "Versão de alta qualidade do Llama 3. Requer mais VRAM.",
        "llama3:8b-instruct-q3_K_M" => "Versão compacta do Llama 3. Menor uso de memória.",
        "phi3:mini" => "Modelo compacto da Microsoft. Rápido e eficiente.",
        "gemma:2b" => "Modelo ultra leve do Google. Ideal para hardware limitado.",
        "mistral:7b-instruct-q4_K_M" => "Modelo europeu de alta qualidade. Ótimo para conversação.",
        "codellama:7b-instruct" => "Especializado em código. Ideal para programadores.",
        _ => "Modelo de IA para processamento local."
    };

    private static string GetPerformanceNote(OllamaModelInfo model, ulong vramMb)
    {
        if (vramMb == 0)
            return "🖥️ Rodará via CPU";

        if (vramMb >= model.MinVramMb * 1.5)
            return "🚀 Performance excelente";

        if (vramMb >= model.MinVramMb)
            return "⚡ Performance boa";

        if (vramMb >= model.MinVramMb * 0.75)
            return "⚠️ Performance limitada";

        return "❌ VRAM insuficiente";
    }
}

/// <summary>
/// Model option for the installer wizard.
/// </summary>
public sealed class InstallerModelOption
{
    public string ModelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ulong SizeMb { get; set; }
    public ulong MinVramMb { get; set; }
    public bool CanRun { get; set; }
    public bool IsRecommended { get; set; }
    public string Description { get; set; } = string.Empty;
    public string PerformanceNote { get; set; } = string.Empty;

    public string SizeDisplay => $"{SizeMb / 1024.0:F1} GB";
    public string VramDisplay => $"{MinVramMb / 1024.0:F1} GB VRAM";
}
