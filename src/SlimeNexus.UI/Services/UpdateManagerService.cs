using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace SlimeNexus.UI.Services;

/// <summary>
/// Service that manages automatic updates from GitHub releases.
/// Uses Velopack for silent background updates.
/// </summary>
public sealed class UpdateManagerService
{
    private readonly ILogger<UpdateManagerService> _logger;
    private readonly UpdateManager _updateManager;
    
    /// <summary>
    /// GitHub repository for updates (format: owner/repo).
    /// </summary>
    private const string GitHubRepo = "ItaloKbb/SlimeNexus";
    
    /// <summary>
    /// Update check interval in hours.
    /// </summary>
    private const int UpdateCheckIntervalHours = 4;

    public UpdateManagerService(ILogger<UpdateManagerService> logger)
    {
        _logger = logger;
        
        // Configure Velopack to check GitHub releases
        var source = new GithubSource($"https://github.com/{GitHubRepo}", null, prerelease: false);
        _updateManager = new UpdateManager(source);
    }

    /// <summary>
    /// Gets whether the app was installed via Velopack installer.
    /// Returns false when running in development mode.
    /// </summary>
    public bool IsInstalled => _updateManager.IsInstalled;

    /// <summary>
    /// Gets the current installed version, or null if not installed.
    /// </summary>
    public string? CurrentVersion => _updateManager.CurrentVersion?.ToString();

    /// <summary>
    /// Gets whether an update is currently available.
    /// </summary>
    public bool UpdateAvailable { get; private set; }

    /// <summary>
    /// Gets the latest available version, if any.
    /// </summary>
    public string? LatestVersion { get; private set; }

    /// <summary>
    /// Event raised when an update is found.
    /// </summary>
    public event EventHandler<UpdateFoundEventArgs>? UpdateFound;

    /// <summary>
    /// Event raised when update progress changes.
    /// </summary>
    public event EventHandler<int>? DownloadProgress;

    /// <summary>
    /// Checks for updates and applies them silently in the background.
    /// The update will be applied on next application restart.
    /// </summary>
    public async Task CheckAndApplyUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
        {
            _logger.LogDebug("Skipping update check - running in development mode (not installed via Velopack)");
            return;
        }

        try
        {
            _logger.LogInformation("Checking for updates from GitHub: {Repo}", GitHubRepo);

            // Check for new version
            var updateInfo = await _updateManager.CheckForUpdatesAsync();
            
            if (updateInfo is null)
            {
                _logger.LogInformation("No updates available. Current version: {Version}", CurrentVersion);
                UpdateAvailable = false;
                return;
            }

            LatestVersion = updateInfo.TargetFullRelease.Version.ToString();
            UpdateAvailable = true;
            
            _logger.LogInformation(
                "Update available: {CurrentVersion} -> {NewVersion}", 
                CurrentVersion, 
                LatestVersion);

            // Notify listeners
            UpdateFound?.Invoke(this, new UpdateFoundEventArgs(CurrentVersion ?? "0.0.0", LatestVersion));

            // Download the update with progress reporting
            _logger.LogInformation("Downloading update...");
            await _updateManager.DownloadUpdatesAsync(
                updateInfo,
                progress => 
                {
                    DownloadProgress?.Invoke(this, progress);
                    if (progress % 25 == 0)
                    {
                        _logger.LogDebug("Download progress: {Progress}%", progress);
                    }
                });

            _logger.LogInformation("Update downloaded. Will be applied on next restart.");

            // Apply update silently - will take effect on next app restart
            // The update is staged, not immediately applied
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check or download updates");
        }
    }

    /// <summary>
    /// Applies any pending update and restarts the application.
    /// </summary>
    public void ApplyUpdateAndRestart()
    {
        if (!IsInstalled)
        {
            _logger.LogWarning("Cannot apply update - not installed via Velopack");
            return;
        }

        try
        {
            _logger.LogInformation("Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply update and restart");
        }
    }

    /// <summary>
    /// Applies any pending update without restarting.
    /// The update will take effect on next manual restart.
    /// </summary>
    public void ApplyUpdateOnExit()
    {
        if (!IsInstalled)
        {
            _logger.LogWarning("Cannot apply update - not installed via Velopack");
            return;
        }

        try
        {
            _logger.LogInformation("Staging update to apply on exit...");
            _updateManager.ApplyUpdatesAndExit(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stage update");
        }
    }

    /// <summary>
    /// Waits for the update manager to finish any pending operations.
    /// </summary>
    public void WaitForExit()
    {
        _updateManager.WaitExitThenApplyUpdates(null);
    }
}

/// <summary>
/// Event args for when an update is found.
/// </summary>
public sealed class UpdateFoundEventArgs : EventArgs
{
    public string CurrentVersion { get; }
    public string NewVersion { get; }

    public UpdateFoundEventArgs(string currentVersion, string newVersion)
    {
        CurrentVersion = currentVersion;
        NewVersion = newVersion;
    }
}
