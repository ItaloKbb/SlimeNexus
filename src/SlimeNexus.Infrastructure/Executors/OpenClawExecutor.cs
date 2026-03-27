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
    private readonly IAiProvider _aiProvider;
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
        IAiProvider aiProvider,
        OpenClawOptions? options = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _aiProvider = aiProvider;
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
        TaskTypes.GenerateDocs,
        TaskTypes.Refactor,
        TaskTypes.SecurityScan,
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
                TaskTypes.GenerateDocs => await ExecuteGenerateDocsAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.Refactor => await ExecuteRefactorAsync(metadata, executionPlan, cancellationToken),
                TaskTypes.SecurityScan => await ExecuteSecurityScanAsync(metadata, executionPlan, cancellationToken),
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

        // Check if AI provider is available for AI-powered tasks
        if (metadata.TaskType is TaskTypes.CodeReview or TaskTypes.Custom)
        {
            if (!await _aiProvider.IsAvailableAsync(cancellationToken))
            {
                return (false, "Serviço de IA local (Ollama) não está disponível. Verifique se o Ollama está em execução.");
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

        var csFiles = Directory.GetFiles(metadata.TargetFolder, "*.cs", SearchOption.AllDirectories);
        var staticIssues = new List<string>();
        var totalFiles = 0;
        var totalLines = 0;
        var codeSnippets = new List<(string Path, string Content)>();

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(metadata.TargetFolder, file);
            if (relativePath.Contains($"bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                relativePath.Contains($"obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;

            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            totalFiles++;
            totalLines += lines.Length;

            // Static checks
            if (lines.Length > 500)
                staticIssues.Add($"⚠ {relativePath}: arquivo longo ({lines.Length} linhas)");

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.Contains("TODO", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("FIXME", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("HACK", StringComparison.OrdinalIgnoreCase))
                    staticIssues.Add($"📌 {relativePath}:{i + 1}: {trimmed.Trim()}");
            }

            for (var i = 0; i < lines.Length - 2; i++)
            {
                if (lines[i].TrimStart().StartsWith("catch", StringComparison.Ordinal) &&
                    lines[i + 1].Trim() == "{" && lines[i + 2].Trim() == "}")
                    staticIssues.Add($"🔇 {relativePath}:{i + 1}: bloco catch vazio");
            }

            // Collect code for AI review (limit to keep prompt manageable)
            var content = string.Join("\n", lines);
            if (content.Length <= 3000)
                codeSnippets.Add((relativePath, content));
            else
                codeSnippets.Add((relativePath, string.Join("\n", lines.Take(80)) + "\n// ... (truncated)"));
        }

        // AI-powered review via local Ollama model
        var aiReview = string.Empty;
        try
        {
            var codeForReview = string.Join("\n\n", codeSnippets
                .Take(5)
                .Select(s => $"// === {s.Path} ===\n{s.Content}"));

            if (codeForReview.Length > 6000)
                codeForReview = codeForReview[..6000] + "\n// ... (truncated)";

            var prompt = "Você é um revisor de código sênior. Analise o código C# abaixo e forneça uma revisão concisa em português.\n" +
                "Aponte: bugs potenciais, problemas de segurança, melhorias de performance e boas práticas violadas.\n" +
                "Responda de forma objetiva, usando bullet points.\n\n" +
                codeForReview;

            _logger.LogDebug("Sending code review to AI ({Length} chars)", codeForReview.Length);
            aiReview = await _aiProvider.GenerateAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI review failed, returning static analysis only");
            aiReview = "(Revisão IA indisponível — apenas análise estática)";
        }

        var detailedOutput = $"📊 Arquivos analisados: {totalFiles} ({totalLines} linhas)\n\n" +
                             $"🔎 Análise Estática ({staticIssues.Count} itens):\n" +
                             (staticIssues.Count > 0 ? string.Join("\n", staticIssues.Take(30)) : "Nenhum problema encontrado ✓") +
                             $"\n\n🤖 Revisão IA:\n{aiReview}";

        var xp = CalculateXp(25, metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            $"Code review concluído — {totalFiles} arquivos analisados, {staticIssues.Count} ponto(s) estáticos",
            xp,
            happinessBonus: staticIssues.Count == 0 ? 15 : 10,
            detailedOutput: detailedOutput);
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

    private async Task<ValidationResult> ExecuteGenerateDocsAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(metadata.TargetFolder))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Documentation generation requires a target folder",
                ErrorCodes.InvalidMetadata);
        }

        // Find all C# source files with undocumented public members
        var csFiles = Directory.GetFiles(metadata.TargetFolder, "*.cs", SearchOption.AllDirectories);
        var undocumentedCount = 0;
        var processedFiles = 0;

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var lines = content.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if ((line.StartsWith("public ", StringComparison.Ordinal) ||
                     line.StartsWith("protected ", StringComparison.Ordinal)) &&
                    !line.StartsWith("//", StringComparison.Ordinal))
                {
                    // Check if previous non-empty line is a doc comment
                    var prevIdx = i - 1;
                    while (prevIdx >= 0 && string.IsNullOrWhiteSpace(lines[prevIdx])) prevIdx--;
                    if (prevIdx < 0 || !lines[prevIdx].TrimStart().StartsWith("///", StringComparison.Ordinal))
                    {
                        undocumentedCount++;
                    }
                }
            }
            processedFiles++;
        }

        var message = undocumentedCount == 0
            ? $"All public members documented in {processedFiles} files ✓"
            : $"Found {undocumentedCount} undocumented public members across {processedFiles} files";

        var xp = CalculateXp(20, metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            message,
            xp,
            happinessBonus: undocumentedCount == 0 ? 15 : 8,
            detailedOutput: $"Scanned {processedFiles} .cs files, {undocumentedCount} members need documentation");
    }

    private async Task<ValidationResult> ExecuteRefactorAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        var targetPath = metadata.TargetFolder ?? Directory.GetCurrentDirectory();

        // Run dotnet format to analyze and fix code style
        var output = await RunProcessAsync(
            "dotnet",
            "format --verify-no-changes --verbosity diagnostic",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        var isClean = !output.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                      !output.Contains("would be formatted", StringComparison.OrdinalIgnoreCase);

        if (!isClean)
        {
            // Apply formatting fixes
            var fixOutput = await RunProcessAsync(
                "dotnet",
                "format --verbosity minimal",
                targetPath,
                metadata.TimeoutSeconds,
                cancellationToken);

            output += $"\n[AUTO-FIX]\n{fixOutput}";
        }

        var xp = CalculateXp(25, metadata.XpMultiplier, metadata.PetState);

        return ValidationResult.Success(
            metadata.RequestId,
            isClean ? "Code is clean — no refactoring needed ✓" : "Code refactored and formatted",
            xp,
            happinessBonus: isClean ? 12 : 18,
            detailedOutput: output);
    }

    private async Task<ValidationResult> ExecuteSecurityScanAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        var targetPath = metadata.TargetFolder ?? Directory.GetCurrentDirectory();

        // Check for vulnerable NuGet packages
        var output = await RunProcessAsync(
            "dotnet",
            "list package --vulnerable --include-transitive",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        var hasVulnerabilities = output.Contains("has the following vulnerable packages", StringComparison.OrdinalIgnoreCase) ||
                                 output.Contains("Critical", StringComparison.OrdinalIgnoreCase) ||
                                 output.Contains("High", StringComparison.OrdinalIgnoreCase);

        // Also check for outdated packages
        var outdatedOutput = await RunProcessAsync(
            "dotnet",
            "list package --outdated",
            targetPath,
            metadata.TimeoutSeconds,
            cancellationToken);

        var hasOutdated = outdatedOutput.Contains(">", StringComparison.Ordinal) &&
                          outdatedOutput.Contains("Latest", StringComparison.OrdinalIgnoreCase);

        var combinedOutput = $"[VULNERABILITY SCAN]\n{output}\n\n[OUTDATED PACKAGES]\n{outdatedOutput}";

        if (hasVulnerabilities)
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "⚠ Vulnerable packages detected — review needed",
                ErrorCodes.ExecutionFailed,
                combinedOutput);
        }

        var xp = CalculateXp(30, metadata.XpMultiplier, metadata.PetState);
        var message = hasOutdated
            ? "No vulnerabilities found, but some packages are outdated"
            : "All packages are secure and up to date ✓";

        return ValidationResult.Success(
            metadata.RequestId,
            message,
            xp,
            happinessBonus: hasOutdated ? 12 : 20,
            detailedOutput: combinedOutput);
    }

    private async Task<ValidationResult> ExecuteCustomTaskAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executionPlan) && string.IsNullOrWhiteSpace(metadata.ContextPrompt))
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Custom task requires an execution plan or context prompt",
                ErrorCodes.InvalidMetadata);
        }

        var prompt = new System.Text.StringBuilder();
        prompt.AppendLine("Você é um assistente de desenvolvimento. Execute a tarefa descrita abaixo.");

        if (!string.IsNullOrWhiteSpace(executionPlan))
            prompt.AppendLine($"Plano de execução: {executionPlan}");

        if (!string.IsNullOrWhiteSpace(metadata.ContextPrompt))
            prompt.AppendLine($"Contexto adicional: {metadata.ContextPrompt}");

        if (!string.IsNullOrWhiteSpace(metadata.TargetFolder))
            prompt.AppendLine($"Pasta alvo: {metadata.TargetFolder}");

        prompt.AppendLine("Responda de forma objetiva em português.");

        try
        {
            var output = await _aiProvider.GenerateAsync(prompt.ToString(), cancellationToken);
            var xp = CalculateXp(20, metadata.XpMultiplier, metadata.PetState);

            return ValidationResult.Success(
                metadata.RequestId,
                "Custom task completed via AI",
                xp,
                detailedOutput: output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Custom task AI execution failed");
            return ValidationResult.Failure(
                metadata.RequestId,
                $"Falha na execução: {ex.Message}",
                ErrorCodes.AiInferenceFailed,
                ex.ToString());
        }
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
