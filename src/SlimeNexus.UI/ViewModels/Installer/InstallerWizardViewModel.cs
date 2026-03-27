using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlimeNexus.UI.Services;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Main ViewModel for the installation wizard.
/// Manages navigation between wizard steps and overall installation state.
/// </summary>
public sealed partial class InstallerWizardViewModel : ViewModelBase
{
    private readonly InstallerService _installerService;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private InstallerStepViewModelBase? _currentStep;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _nextButtonText = "Próximo";

    [ObservableProperty]
    private bool _installationComplete;

    public ObservableCollection<InstallerStepViewModelBase> Steps { get; } = [];

    // Results collected during wizard
    public SystemRequirementsResult? SystemRequirements { get; private set; }
    public BenchmarkResult? BenchmarkResults { get; private set; }
    public string SelectedModel { get; set; } = InstallerService.DefaultOllamaModel;

    public InstallerWizardViewModel(InstallerService installerService)
    {
        _installerService = installerService;
        InitializeSteps();
    }

    private async void InitializeSteps()
    {
        Steps.Add(new WelcomeStepViewModel(this));
        Steps.Add(new SystemRequirementsStepViewModel(this, _installerService));
        Steps.Add(new BenchmarkStepViewModel(this, _installerService));
        Steps.Add(new MemoryAnalysisStepViewModel(this));
        Steps.Add(new ModelSelectionStepViewModel(this, _installerService));
        Steps.Add(new PermissionsStepViewModel(this));
        Steps.Add(new InstallationStepViewModel(this, _installerService));
        Steps.Add(new CompletionStepViewModel(this));

        CurrentStepIndex = 0;
        CurrentStep = Steps[0];
        UpdateNavigationState();

        // Call OnEnteringAsync for the first step
        await CurrentStep.OnEnteringAsync();
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (CurrentStep is null) return;

        // Validate current step before proceeding
        if (!await CurrentStep.ValidateAsync())
        {
            return;
        }

        // Execute step completion logic
        await CurrentStep.OnLeavingAsync();

        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
            CurrentStep = Steps[CurrentStepIndex];
            await CurrentStep.OnEnteringAsync();
            UpdateNavigationState();
        }
        else
        {
            // Last step — finish the wizard
            Finish();
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            CurrentStep = Steps[CurrentStepIndex];
            await CurrentStep.OnEnteringAsync();
            UpdateNavigationState();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Request close - will be handled by the window
        OnCancelRequested?.Invoke();
    }

    [RelayCommand]
    private void Finish()
    {
        InstallationComplete = true;
        OnFinishRequested?.Invoke();
    }

    public void UpdateNavigationState()
    {
        CanGoBack = CurrentStepIndex > 0 && 
                    CurrentStep?.CanGoBack == true && 
                    !IsInstalling;
        
        CanGoNext = CurrentStep?.CanGoNext == true && !IsInstalling;

        NextButtonText = CurrentStep switch
        {
            InstallationStepViewModel => "Instalar",
            CompletionStepViewModel => "Concluir",
            _ => "Próximo"
        };
    }

    public void SetSystemRequirements(SystemRequirementsResult result)
    {
        SystemRequirements = result;
    }

    public void SetBenchmarkResults(BenchmarkResult result)
    {
        BenchmarkResults = result;
    }

    public event Action? OnCancelRequested;
    public event Action? OnFinishRequested;
}
