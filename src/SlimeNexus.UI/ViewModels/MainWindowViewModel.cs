using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages tab navigation and overall app state.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isHardwareSelected;

    [ObservableProperty]
    private bool _isPetSelected;

    [ObservableProperty]
    private bool _isChatSelected;

    [ObservableProperty]
    private bool _isTasksSelected;

    [ObservableProperty]
    private bool _isAiOnline;

    [ObservableProperty]
    private string _statusText = "Verificando...";

    public MainWindowViewModel(
        IAiProvider aiProvider,
        ILogger<MainWindowViewModel> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;

        _ = CheckStatusAsync();
    }

    partial void OnIsHomeSelectedChanged(bool value)
    {
        if (value) _logger.LogDebug("Navigating to: home");
    }

    partial void OnIsHardwareSelectedChanged(bool value)
    {
        if (value) _logger.LogDebug("Navigating to: hardware");
    }

    partial void OnIsPetSelectedChanged(bool value)
    {
        if (value) _logger.LogDebug("Navigating to: pet");
    }

    partial void OnIsChatSelectedChanged(bool value)
    {
        if (value) _logger.LogDebug("Navigating to: chat");
    }

    partial void OnIsTasksSelectedChanged(bool value)
    {
        if (value) _logger.LogDebug("Navigating to: tasks");
    }

    [RelayCommand]
    private void GoToChat()
    {
        IsHomeSelected = false;
        IsHardwareSelected = false;
        IsPetSelected = false;
        IsTasksSelected = false;
        IsChatSelected = true;
    }

    [RelayCommand]
    private void GoToHardware()
    {
        IsHomeSelected = false;
        IsChatSelected = false;
        IsPetSelected = false;
        IsTasksSelected = false;
        IsHardwareSelected = true;
    }

    [RelayCommand]
    private void GoToPet()
    {
        IsHomeSelected = false;
        IsHardwareSelected = false;
        IsChatSelected = false;
        IsTasksSelected = false;
        IsPetSelected = true;
    }

    private async Task CheckStatusAsync()
    {
        try
        {
            IsAiOnline = await _aiProvider.IsAvailableAsync();
            StatusText = IsAiOnline ? "AI Online" : "AI Offline";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check AI status");
            StatusText = "Erro";
        }
    }
}
