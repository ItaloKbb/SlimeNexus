using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.UI.ViewModels;

/// <summary>
/// ViewModel for the Hardware Dashboard view.
/// Displays system hardware information and AI inference capabilities.
/// </summary>
public partial class HardwareDashboardViewModel : ViewModelBase
{
    private readonly IHardwareProber _hardwareProber;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<HardwareDashboardViewModel> _logger;

    // GPU Info
    [ObservableProperty]
    private string _gpuName = "Loading...";

    [ObservableProperty]
    private ulong _vramTotalMb;

    [ObservableProperty]
    private ulong _vramAvailableMb;

    [ObservableProperty]
    private double _vramUsagePercent;

    [ObservableProperty]
    private bool _supportsCuda;

    // CPU Info
    [ObservableProperty]
    private string _cpuName = "Loading...";

    [ObservableProperty]
    private int _cpuCores;

    // RAM Info
    [ObservableProperty]
    private ulong _ramTotalMb;

    [ObservableProperty]
    private ulong _ramAvailableMb;

    [ObservableProperty]
    private double _ramUsagePercent;

    // AI Inference
    [ObservableProperty]
    private bool _canRunLocalInference;

    [ObservableProperty]
    private string _suggestedQuantization = "Unknown";

    [ObservableProperty]
    private string _recommendedModel = "Unknown";

    [ObservableProperty]
    private bool _isAiAvailable;

    [ObservableProperty]
    private string _aiProviderName = "Unknown";

    // Last Update
    [ObservableProperty]
    private DateTime _lastUpdated;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // Refresh Timer
    private readonly System.Timers.Timer _refreshTimer;
    private bool _autoRefresh = true;

    public HardwareDashboardViewModel(
        IHardwareProber hardwareProber,
        IAiProvider aiProvider,
        ILogger<HardwareDashboardViewModel> logger)
    {
        _hardwareProber = hardwareProber;
        _aiProvider = aiProvider;
        _logger = logger;

        // Setup auto-refresh timer (every 30 seconds)
        _refreshTimer = new System.Timers.Timer(30_000);
        _refreshTimer.Elapsed += async (_, _) => await RefreshAsync();
        _refreshTimer.AutoReset = true;
        
        // Initial load
        _ = RefreshAsync();
    }

    /// <summary>
    /// Gets or sets whether auto-refresh is enabled.
    /// </summary>
    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (SetProperty(ref _autoRefresh, value))
            {
                if (value)
                    _refreshTimer.Start();
                else
                    _refreshTimer.Stop();
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;

        SetBusy(true, "Refreshing hardware info...");
        StatusMessage = "Refreshing...";

        try
        {
            _logger.LogDebug("Refreshing hardware information");

            // Get hardware specs
            var specs = await _hardwareProber.GetSpecsAsync();
            UpdateHardwareInfo(specs);

            // Check AI availability
            IsAiAvailable = await _aiProvider.IsAvailableAsync();
            AiProviderName = _aiProvider.ProviderName;

            LastUpdated = DateTime.Now;
            StatusMessage = IsAiAvailable 
                ? "✅ All systems operational" 
                : "⚠️ AI service offline";

            _logger.LogInformation(
                "Hardware refreshed: GPU={Gpu}, VRAM={Vram}MB, AI={AiStatus}",
                GpuName, VramTotalMb, IsAiAvailable ? "Online" : "Offline");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh hardware info");
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UpdateHardwareInfo(HardwareSpecs specs)
    {
        // GPU
        GpuName = specs.GpuName;
        VramTotalMb = specs.VramTotalMb;
        VramAvailableMb = specs.VramAvailableMb;
        VramUsagePercent = specs.VramTotalMb > 0 
            ? 100.0 * (specs.VramTotalMb - specs.VramAvailableMb) / specs.VramTotalMb 
            : 0;
        SupportsCuda = specs.SupportsCuda;

        // CPU
        CpuName = specs.CpuName;
        CpuCores = specs.CpuCoreCount;

        // RAM
        RamTotalMb = specs.RamTotalMb;
        RamAvailableMb = specs.RamAvailableMb;
        RamUsagePercent = specs.RamTotalMb > 0 
            ? 100.0 * (specs.RamTotalMb - specs.RamAvailableMb) / specs.RamTotalMb 
            : 0;

        // AI Inference
        CanRunLocalInference = specs.CanRunLocalInference;
        SuggestedQuantization = specs.SuggestedQuantization;
        RecommendedModel = GetRecommendedModelDisplay(specs);
    }

    private static string GetRecommendedModelDisplay(HardwareSpecs specs)
    {
        return specs.VramAvailableMb switch
        {
            >= 24576 => "llama3:70b (Full)",
            >= 16384 => "llama3:70b-q2_K",
            >= 12288 => "llama3:8b-q8_0",
            >= 8192 => "llama3:8b-q4_K_M ⭐",
            >= 6144 => "llama3:8b-q4_0",
            >= 4096 => "llama3:8b-q2_K",
            _ => "phi3:mini (Low VRAM)"
        };
    }

    /// <summary>
    /// Formats bytes to human-readable string.
    /// </summary>
    public static string FormatBytes(ulong megabytes)
    {
        if (megabytes >= 1024)
            return $"{megabytes / 1024.0:F1} GB";
        return $"{megabytes} MB";
    }
}
