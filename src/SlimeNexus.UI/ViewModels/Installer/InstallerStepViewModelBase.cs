using CommunityToolkit.Mvvm.ComponentModel;

namespace SlimeNexus.UI.ViewModels.Installer;

/// <summary>
/// Base class for all installer wizard steps.
/// </summary>
public abstract partial class InstallerStepViewModelBase : ViewModelBase
{
    protected readonly InstallerWizardViewModel Wizard;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _icon = "⚙️";

    [ObservableProperty]
    private bool _canGoBack = true;

    [ObservableProperty]
    private bool _canGoNext = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    protected InstallerStepViewModelBase(InstallerWizardViewModel wizard)
    {
        Wizard = wizard;
    }

    /// <summary>
    /// Called when CanGoNext changes - notifies the wizard to update navigation state.
    /// </summary>
    partial void OnCanGoNextChanged(bool value) => UpdateWizardNavigation();

    /// <summary>
    /// Called when CanGoBack changes - notifies the wizard to update navigation state.
    /// </summary>
    partial void OnCanGoBackChanged(bool value) => UpdateWizardNavigation();

    /// <summary>
    /// Called when entering this step.
    /// </summary>
    public virtual Task OnEnteringAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when leaving this step (before moving to next).
    /// </summary>
    public virtual Task OnLeavingAsync() => Task.CompletedTask;

    /// <summary>
    /// Validates if the user can proceed to the next step.
    /// </summary>
    public virtual Task<bool> ValidateAsync() => Task.FromResult(true);

    protected void UpdateWizardNavigation() => Wizard.UpdateNavigationState();
}
