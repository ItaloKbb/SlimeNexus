using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.UI.Views;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// ViewModel for the system tray icon and its context menu.
/// </summary>
public partial class TrayIconViewModel : ViewModelBase
{
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<TrayIconViewModel> _logger;

    [ObservableProperty]
    private string _trayTooltip = "SlimeNexus - Your AI Pet Assistant";

    [ObservableProperty]
    private string _aiStatusText = "⏳ Checking...";

    [ObservableProperty]
    private bool _isAiConnected;

    public TrayIconViewModel(
        IAiProvider aiProvider,
        ILogger<TrayIconViewModel> logger)
    {
        _aiProvider = aiProvider;
        _logger = logger;

        // Check AI status on startup
        _ = CheckAiStatusAsync();
    }

    [RelayCommand]
    private void OpenDashboard()
    {
        _logger.LogDebug("Opening main dashboard");

        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();
    }

    [RelayCommand]
    private void OpenPetStatus()
    {
        _logger.LogDebug("Opening pet status");

        var petStatusView = App.Services.GetRequiredService<PetStatusView>();
        petStatusView.Show();
        petStatusView.Activate();
    }

    [RelayCommand]
    private void OpenHardware()
    {
        _logger.LogDebug("Opening hardware dashboard");

        var hardwareWindow = App.Services.GetRequiredService<HardwareDashboardWindow>();
        hardwareWindow.Show();
        hardwareWindow.Activate();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _logger.LogDebug("Opening settings");
        // TODO: Implement settings window
    }

    [RelayCommand]
    private async Task CheckAiStatusAsync()
    {
        _logger.LogDebug("Checking AI status...");
        AiStatusText = "⏳ Checking...";

        try
        {
            IsAiConnected = await _aiProvider.IsAvailableAsync();
            AiStatusText = IsAiConnected
                ? $"✅ Connected ({_aiProvider.ProviderName})"
                : "❌ Not Connected";
            
            TrayTooltip = IsAiConnected
                ? $"SlimeNexus - AI Ready ({_aiProvider.ProviderName})"
                : "SlimeNexus - AI Offline";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check AI status");
            AiStatusText = "❌ Error";
            IsAiConnected = false;
        }
    }

    [RelayCommand]
    private void Exit()
    {
        _logger.LogInformation("User requested exit");

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
