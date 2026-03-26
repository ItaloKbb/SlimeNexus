using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// System requirements check step.
/// </summary>
public sealed partial class SystemRequirementsStepViewModel : InstallerStepViewModelBase
{
    private readonly InstallerService _installerService;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _checkComplete;

    // Hardware info
    [ObservableProperty]
    private string _cpuName = "Verificando...";

    [ObservableProperty]
    private int _cpuCores;

    [ObservableProperty]
    private string _gpuName = "Verificando...";

    [ObservableProperty]
    private string _ramInfo = "Verificando...";

    [ObservableProperty]
    private string _vramInfo = "Verificando...";

    [ObservableProperty]
    private string _diskInfo = "Verificando...";

    // Requirements status
    [ObservableProperty]
    private bool _meetsRamRequirement;

    [ObservableProperty]
    private bool _meetsDiskRequirement;

    [ObservableProperty]
    private bool _meetsVramRequirement;

    [ObservableProperty]
    private bool _meetsAllRequirements;

    [ObservableProperty]
    private bool _supportsCuda;

    [ObservableProperty]
    private string _overallStatus = "Aguardando verificação...";

    [ObservableProperty]
    private string _overallStatusColor = "Orange";

    public SystemRequirementsStepViewModel(
        InstallerWizardViewModel wizard,
        InstallerService installerService) : base(wizard)
    {
        _installerService = installerService;
        Title = "Requisitos do Sistema";
        Description = "Verificando se seu computador atende aos requisitos mínimos";
        Icon = "🖥️";
        CanGoNext = false; // Can't proceed until check is complete
    }

    public override async Task OnEnteringAsync()
    {
        if (!CheckComplete)
        {
            await CheckRequirementsAsync();
        }
    }

    [RelayCommand]
    private async Task CheckRequirementsAsync()
    {
        IsChecking = true;
        IsBusy = true;
        CanGoNext = false;
        StatusMessage = "Analisando hardware...";

        try
        {
            var result = await _installerService.VerifySystemRequirementsAsync();

            // Update UI with results
            CpuName = result.CpuName;
            CpuCores = result.CpuCores;
            GpuName = result.GpuName;

            var totalRamGb = result.TotalRamMb / 1024.0;
            var availRamGb = result.AvailableRamMb / 1024.0;
            RamInfo = $"{totalRamGb:F1} GB total ({availRamGb:F1} GB disponível)";

            var totalVramGb = result.TotalVramMb / 1024.0;
            VramInfo = result.TotalVramMb > 0 
                ? $"{totalVramGb:F1} GB VRAM" 
                : "GPU integrada / Sem VRAM dedicada";

            var availDiskGb = result.AvailableDiskSpaceMb / 1024.0;
            DiskInfo = $"{availDiskGb:F1} GB disponível";

            MeetsRamRequirement = result.MeetsRamRequirement;
            MeetsDiskRequirement = result.MeetsDiskRequirement;
            MeetsVramRequirement = result.MeetsVramRequirement;
            MeetsAllRequirements = result.MeetsAllRequirements;
            SupportsCuda = result.SupportsCuda;

            // Store results in wizard
            Wizard.SetSystemRequirements(result);

            // Update overall status
            if (MeetsAllRequirements)
            {
                OverallStatus = "✅ Seu sistema atende a todos os requisitos!";
                OverallStatusColor = "Green";
                CanGoNext = true;
            }
            else if (MeetsRamRequirement && MeetsDiskRequirement)
            {
                OverallStatus = "⚠️ Sistema compatível, mas com limitações de GPU";
                OverallStatusColor = "Orange";
                CanGoNext = true; // Allow to proceed with warnings
            }
            else
            {
                OverallStatus = "❌ Seu sistema não atende aos requisitos mínimos";
                OverallStatusColor = "Red";
                CanGoNext = false;
            }

            CheckComplete = true;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            OverallStatus = $"❌ Erro na verificação: {ex.Message}";
            OverallStatusColor = "Red";
        }
        finally
        {
            IsChecking = false;
            IsBusy = false;
            UpdateWizardNavigation();
        }
    }

    // Requirement descriptions for UI
    public string MinRamDescription => $"Mínimo {InstallerService.MinRamMb / 1024} GB RAM";
    public string MinDiskDescription => $"Mínimo {InstallerService.MinDiskSpaceMb / 1024} GB espaço livre";
    public string MinVramDescription => $"Recomendado {InstallerService.MinVramMb / 1024} GB VRAM";
}
