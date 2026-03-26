using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Enums;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Infrastructure.Executors;

/// <summary>
/// Task executor that interfaces with OpenClaw for sandboxed command execution.
/// Uses System.Diagnostics.Process to communicate with the OpenClaw gateway.
/// </summary>
public sealed class OpenClawExecutor : ITaskExecutor
{
    private readonly ILogger<OpenClawExecutor> _logger;
    private readonly OpenClawOptions _options;
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OpenClawExecutor(
        ILogger<OpenClawExecutor> logger,
        HttpClient httpClient,
        OpenClawOptions? options = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options ?? new OpenClawOptions();

        _httpClient.BaseAddress ??= new Uri(_options.GatewayUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.DefaultTimeoutSeconds);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedTaskTypes =>
    [
        TaskTypes.CodeReview,
        TaskTypes.FileCleanup,
        TaskTypes.GitCommit,
        TaskTypes.TestRun,
        TaskTypes.BuildProject,
        TaskTypes.Custom
    ];

    /// <inheritdoc />
    public bool CanExecute(string taskType) =>
        SupportedTaskTypes.Contains(taskType, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public async Task<ValidationResult> ExecuteAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Executing task {TaskType} for request {RequestId}",
            metadata.TaskType, metadata.RequestId);

        try
        {
            // Validate preconditions first
            var (isValid, errorMessage) = await ValidatePreconditionsAsync(metadata, cancellationToken);
            if (!isValid)
            {
                return ValidationResult.Failure(
                    metadata.RequestId,
                    errorMessage ?? "Precondition validation failed",
                    ErrorCodes.PreconditionFailed,
                    executionTimeMs: stopwatch.ElapsedMilliseconds);
            }

            // Route to appropriate executor based on task type
            var result = metadata.TaskType switch
            {
                TaskTypes.CodeReview => await ExecuteCodeReviewAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.FileCleanup => await ExecuteFileCleanupAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.GitCommit => await ExecuteGitCommitValidationAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.TestRun => await ExecuteTestRunAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.BuildProject => await ExecuteBuildProjectAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.Custom => await ExecuteCustomTaskAsync(metadata, executionPlan, cancellationToken),
                _ => throw new NotSupportedException($"Task type '{metadata.TaskType}' is not supported")
            };

            stopwatch.Stop();

            // Adjust result with execution time
            return result with { ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Task execution was cancelled",
                ErrorCodes.TaskCancelled,
                executionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                $"Task execution timed out after {metadata.TimeoutSeconds} seconds",
                ErrorCodes.ExecutionTimeout,
                executionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed for {RequestId}", metadata.RequestId);
            return ValidationResult.Failure(
                metadata.RequestId,
                $"Execution failed: {ex.Message}",
                ErrorCodes.ExecutionFailed,
                ex.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc />
    public async Task<(bool IsValid, string? ErrorMessage)> ValidatePreconditionsAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        // Check if pet can perform tasks
        if (!PetStates.CanPerformTasks(metadata.PetState))
        {
            return (false, $"Pet cannot perform tasks while {metadata.PetState}");
        }

        // Check target folder exists for file-based operations
        if (metadata.TargetFolder is not null)
        {
            if (!Directory.Exists(metadata.TargetFolder))
            {
                return (false, $"Target folder does not exist: {metadata.TargetFolder}");
            }

            // Check read permissions
            try
            {
                _ = Directory.GetFiles(metadata.TargetFolder, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException)
            {
                return (false, $"Permission denied to access: {metadata.TargetFolder}");
            }
        }

        // Check OpenClaw gateway availability (if using gateway mode)
        if (_options.UseGateway)
        {
            var isAvailable = await CheckGatewayAvailabilityAsync(cancellationToken);
            if (!isAvailable)
            {
                return (false, "OpenClaw gateway is not available");
            }
        }

        return (true, null);
    }

    #region Task-Specific Executors

    private async Task<ValidationResult> ExecuteCodeReviewAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.TargetFolder))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Code review requires a target folder",
                ErrorCodes.InvalidMetadata);
        }

        // Execute via OpenClaw gateway or direct process
        var command = BuildOpenClawCommand("code-review", new
        {
            path = metadata.TargetFolder,
            plan = executionPlan,
            context = metadata.ContextPrompt
        });

        var output = await ExecuteCommandAsync(command, metadata.TimeoutSeconds, cancellationToken);

        // Parse output to determine success
        var success = output.Contains("✓", StringComparison.Ordinal) || 
                      output.Contains("passed", StringComparison.OrdinalIgnoreCase);

        var xp = success ? CalculateXp(25, metadata.XpMultiplier, metadata.PetState) : 0;

        return success
            ? ValidationResult.Success(metadata.RequestId, "Code review completed", xp, detailedOutput: output)
            : ValidationResult.Failure(metadata.RequestId, "Code review found issues", ErrorCodes.ExecutionFailed, output);
    }

    private async Task<ValidationResult> ExecuteFileCleanupAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.TargetFolder))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "File cleanup requires a target folder",
                ErrorCodes.InvalidMetadata);
        }

        // Cleanup patterns: bin, obj, node_modules, .vs, etc.
        var cleanupPatterns = new[] { "bin", "obj", "node_modules", ".vs", "packages", "TestResults" };
        var deletedCount = 0;
        var freedBytes = 0L;

        foreach (var pattern in cleanupPatterns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirs = Directory.GetDirectories(
                metadata.TargetFolder, 
                pattern, 
                SearchOption.AllDirectories);

            foreach (var dir in dirs)
            {
                try
                {
                    var size = GetDirectorySize(dir);
                    Directory.Delete(dir, recursive: true);
                    deletedCount++;
                    freedBytes += size;
                    _logger.LogDebug("Deleted: {Directory} ({Size} bytes)", dir, size);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete {Directory}", dir);
                }
            }
        }

        var freedMb = freedBytes / (1024.0 * 1024.0);
        var message = $"Cleaned up {deletedCount} directories, freed {freedMb:F2} MB";
        var xp = CalculateXp(15 + (deletedCount * 2), metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            message,
            xp,
            happinessBonus: Math.Min(20, 5 + deletedCount),
            detailedOutput: $"Patterns: {string.Join(", ", cleanupPatterns)}");
    }

    private async Task<ValidationResult> ExecuteGitCommitValidationAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        var targetPath = metadata.TargetFolder ?? Directory.GetCurrentDirectory();

        // Check if it's a git repository
        var gitDir = Path.Combine(targetPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Not a Git repository",
                ErrorCodes.PreconditionFailed);
        }

        // Get last commit info
        var output = await RunProcessAsync(
            "git",
            "log -1 --pretty=format:\"%H|%s|%an|%ar\"",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "No commits found in repository",
                ErrorCodes.ExecutionFailed);
        }

        var parts = output.Split('|');
        var commitMessage = parts.Length > 1 ? parts[1] : "Unknown";

        // Validate commit message format (conventional commits)
        var isValidFormat = ValidateConventionalCommit(commitMessage);
        var xp = isValidFormat 
            ? CalculateXp(20, metadata.XpMultiplier, metadata.PetState) 
            : CalculateXp(10, metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            $"Git commit validated: {commitMessage}",
            xp,
            happinessBonus: isValidFormat ? 15 : 8,
            detailedOutput: output);
    }

    private async Task<ValidationResult> ExecuteTestRunAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        var targetPath = metadata.TargetFolder ?? Directory.GetCurrentDirectory();

        // Run dotnet test
        var output = await RunProcessAsync(
            "dotnet",
            "test --no-build --verbosity minimal",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        var passed = output.Contains("Passed!", StringComparison.OrdinalIgnoreCase) ||
                     output.Contains("Test Run Successful", StringComparison.OrdinalIgnoreCase);

        var xp = passed ? CalculateXp(30, metadata.XpMultiplier, metadata.PetState) : 0;

        return passed
            ? ValidationResult.Success(metadata.RequestId, "All tests passed!", xp, happinessBonus: 20, detailedOutput: output)
            : ValidationResult.Failure(metadata.RequestId, "Some tests failed", ErrorCodes.ExecutionFailed, output);
    }

    private async Task<ValidationResult> ExecuteBuildProjectAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        var targetPath = metadata.TargetFolder ?? Directory.GetCurrentDirectory();

        var output = await RunProcessAsync(
            "dotnet",
            "build --no-incremental --verbosity minimal",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        var succeeded = output.Contains("Build succeeded", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("0 Error(s)", StringComparison.OrdinalIgnoreCase);

        var xp = succeeded ? CalculateXp(15, metadata.XpMultiplier, metadata.PetState) : 0;

        return succeeded
            ? ValidationResult.Success(metadata.RequestId, "Build succeeded!", xp, detailedOutput: output)
            : ValidationResult.Failure(metadata.RequestId, "Build failed", ErrorCodes.ExecutionFailed, output);
    }

    private async Task<ValidationResult> ExecuteCustomTaskAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        // Parse execution plan for custom command
        if (string.IsNullOrWhiteSpace(executionPlan))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Custom task requires an execution plan",
                ErrorCodes.InvalidMetadata);
        }

        // For security, custom tasks must go through OpenClaw gateway
        if (!_options.UseGateway)
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Custom tasks require OpenClaw gateway to be enabled",
                ErrorCodes.PreconditionFailed);
        }

        var command = BuildOpenClawCommand("execute", new
        {
            plan = executionPlan,
            context = metadata.ContextPrompt,
            timeout = metadata.TimeoutSeconds
        });

        var output = await ExecuteCommandAsync(command, metadata.TimeoutSeconds, cancellationToken);
        var xp = CalculateXp(20, metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            "Custom task completed",
            xp,
            detailedOutput: output);
    }

    #endregion

    #region Helper Methods

    private string BuildOpenClawCommand(string action, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return $"openclaw {action} --payload \"{json.Replace("\"", "\\\"")}\"";
    }

    private async Task<string> ExecuteCommandAsync(
        string command,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (_options.UseGateway)
        {
            return await ExecuteViaGatewayAsync(command, timeoutSeconds, cancellationToken);
        }

        // Direct execution (less secure, for development)
        var parts = command.Split(' ', 2);
        return await RunProcessAsync(
            parts[0],
            parts.Length > 1 ? parts[1] : "",
            null,
            timeoutSeconds,
            cancellationToken);
    }

    private async Task<string> ExecuteViaGatewayAsync(
        string command,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var request = new { command, timeout = timeoutSeconds };
        var response = await _httpClient.PostAsJsonAsync(
            "api/execute",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<bool> CheckGatewayAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunProcessAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        var output = await outputTask;
        var error = await errorTask;

        return string.IsNullOrWhiteSpace(error) ? output : $"{output}\n[STDERR]\n{error}";
    }

    private static int CalculateXp(int baseXp, float multiplier, string petState)
    {
        var stateMultiplier = PetStates.GetXpMultiplier(petState);
        return (int)(baseXp * multiplier * stateMultiplier);
    }

    private static bool ValidateConventionalCommit(string message)
    {
        // Conventional commit format: type(scope): description
        var patterns = new[] { "feat", "fix", "docs", "style", "refactor", "test", "chore", "perf", "ci", "build" };
        return patterns.Any(p => message.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }

    #endregion
}

/// <summary>
/// Configuration options for OpenClaw executor.
/// </summary>
public sealed record OpenClawOptions
{
    /// <summary>URL of the OpenClaw gateway API.</summary>
    public string GatewayUrl { get; init; } = "http://localhost:8080";

    /// <summary>Whether to use the gateway for command execution (recommended for security).</summary>
    public bool UseGateway { get; init; } = true;

    /// <summary>Default timeout for task execution in seconds.</summary>
    public int DefaultTimeoutSeconds { get; init; } = 120;

    /// <summary>Maximum allowed timeout in seconds.</summary>
    public int MaxTimeoutSeconds { get; init; } = 600;
}
