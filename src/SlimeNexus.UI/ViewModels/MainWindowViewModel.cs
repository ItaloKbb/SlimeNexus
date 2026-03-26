using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
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
    private bool _isTasksSelected;

    [ObservableProperty]
    private bool _isAiOnline;

    [ObservableProperty]
    private string _statusText = "Checking...";

    [ObservableProperty]
    private object? _currentView;

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
        if (value) NavigateTo("home");
    }

    partial void OnIsHardwareSelectedChanged(bool value)
    {
        if (value) NavigateTo("hardware");
    }

    partial void OnIsPetSelectedChanged(bool value)
    {
        if (value) NavigateTo("pet");
    }

    partial void OnIsTasksSelectedChanged(bool value)
    {
        if (value) NavigateTo("tasks");
    }

    private void NavigateTo(string view)
    {
        _logger.LogDebug("Navigating to: {View}", view);
        // Views will be set by the ContentControl DataTemplate
        CurrentView = view;
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
            StatusText = "Error";
        }
    }
}
