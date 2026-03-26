using System.Diagnostics;
using SlimeNexus.Core.Domain.Enums;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Api.Services;

/// <summary>
/// The AI orchestration "brain" that decides which model/executor to use
/// based on task type and hardware capabilities.
/// </summary>
public sealed class AiOrchestrator : IAiOrchestrator
{
    private readonly IAiProvider _aiProvider;
    private readonly IHardwareProber _hardwareProber;
    private readonly ITaskExecutorRegistry _executorRegistry;
    private readonly ILogger<AiOrchestrator> _logger;

    private HardwareSpecs? _cachedSpecs;
    private string _currentModel = "llama3:8b-instruct-q4_K_M";

    public AiOrchestrator(
        IAiProvider aiProvider,
        IHardwareProber hardwareProber,
        ITaskExecutorRegistry executorRegistry,
        ILogger<AiOrchestrator> logger)
    {
        _aiProvider = aiProvider;
        _hardwareProber = hardwareProber;
        _executorRegistry = executorRegistry;
        _logger = logger;
    }

    /// <inheritdoc />
    public string CurrentModelName => _currentModel;

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        var isAiReady = await _aiProvider.IsAvailableAsync(cancellationToken);
        
        if (!isAiReady)
        {
            _logger.LogWarning("AI provider is not available");
            return false;
        }

        // Cache hardware specs on first ready check
        _cachedSpecs ??= await _hardwareProber.GetSpecsAsync(cancellationToken);

        // Update model based on hardware
        _currentModel = GetRecommendedModel(_cachedSpecs.Value);
        
        _logger.LogDebug("AI Orchestrator ready. Using model: {Model}", _currentModel);
        return true;
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ProcessTaskAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Processing task {RequestId}: {TaskType} (pet: {PetState})",
            metadata.RequestId, metadata.TaskType, metadata.PetState);

        try
        {
            // Validate metadata
            if (!metadata.IsValid)
            {
                return ValidationResult.Failure(
                    metadata.RequestId,
                    "Invalid task metadata",
                    ErrorCodes.InvalidMetadata,
                    executionTimeMs: stopwatch.ElapsedMilliseconds);
            }

            // Check if pet can perform tasks
            if (!PetStates.CanPerformTasks(metadata.PetState))
            {
                return ValidationResult.Failure(
                    metadata.RequestId,
                    $"Pet cannot perform tasks while {metadata.PetState}",
                    ErrorCodes.PetCannotPerformTask,
                    executionTimeMs: stopwatch.ElapsedMilliseconds);
            }

            // Find appropriate executor
            var executor = _executorRegistry.GetExecutor(metadata.TaskType);
            if (executor is null)
            {
                return ValidationResult.Failure(
                    metadata.RequestId,
                    $"No executor found for task type: {metadata.TaskType}",
                    ErrorCodes.ExecutorNotFound,
                    executionTimeMs: stopwatch.ElapsedMilliseconds);
            }

            // Generate execution plan using AI
            var executionPlan = await GenerateExecutionPlanAsync(metadata, cancellationToken);
            
            _logger.LogDebug("Execution plan generated: {Plan}", 
                executionPlan[..Math.Min(200, executionPlan.Length)]);

            // Execute the task
            var result = await executor.ExecuteAsync(metadata, executionPlan, cancellationToken);

            stopwatch.Stop();
            
            _logger.LogInformation(
                "Task {RequestId} completed: Success={Success}, XP={XP}, Time={Time}ms",
                metadata.RequestId, result.IsSuccess, result.XpEarned, stopwatch.ElapsedMilliseconds);

            return result with { ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            return ValidationResult.Failure(
                metadata.RequestId,
                "Task was cancelled",
                ErrorCodes.TaskCancelled,
                executionTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {RequestId} failed with exception", metadata.RequestId);
            
            return ValidationResult.Failure(
                metadata.RequestId,
                $"Task failed: {ex.Message}",
                ErrorCodes.UnexpectedError,
                ex.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc />
    public string GetRecommendedModel(HardwareSpecs specs)
    {
        // Choose model based on available VRAM
        return specs.VramAvailableMb switch
        {
            >= 24576 => "llama3:70b-instruct-q4_K_M",  // 24GB+ VRAM
            >= 16384 => "llama3:70b-instruct-q2_K",   // 16GB+ VRAM
            >= 12288 => "llama3:8b-instruct-q8_0",    // 12GB+ VRAM
            >= 8192 => "llama3:8b-instruct-q4_K_M",   // 8GB+ VRAM (default)
            >= 6144 => "llama3:8b-instruct-q4_0",     // 6GB+ VRAM
            >= 4096 => "llama3:8b-instruct-q2_K",     // 4GB+ VRAM
            _ => "phi3:mini"                           // Low VRAM fallback
        };
    }

    /// <inheritdoc />
    public async Task<string> GenerateExecutionPlanAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt(metadata.TaskType);
        var userPrompt = BuildUserPrompt(metadata);

        var fullPrompt = $"""
            {systemPrompt}

            User Request:
            {userPrompt}

            Generate a structured execution plan in JSON format.
            """;

        try
        {
            var plan = await _aiProvider.GenerateAsync(fullPrompt, cancellationToken);
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate AI execution plan, using default");
            return GenerateDefaultPlan(metadata);
        }
    }

    #region Prompt Building

    private static string BuildSystemPrompt(string taskType) => taskType switch
    {
        TaskTypes.CodeReview => """
            You are a code review assistant. Analyze code for:
            - Code quality and best practices
            - Potential bugs and security issues
            - Performance improvements
            - Documentation and readability
            Respond with actionable suggestions.
            """,

        TaskTypes.FileCleanup => """
            You are a file system assistant. Identify:
            - Build artifacts (bin, obj, node_modules)
            - Temporary files and caches
            - Large unnecessary files
            Respond with cleanup recommendations.
            """,

        TaskTypes.GitCommit => """
            You are a Git workflow assistant. Validate:
            - Commit message follows conventional commits
            - Changes are atomic and well-scoped
            - No sensitive data in commits
            """,

        TaskTypes.TestRun => """
            You are a testing assistant. Analyze:
            - Test coverage and quality
            - Failed test reasons
            - Suggestions for additional tests
            """,

        _ => """
            You are a helpful development assistant. 
            Analyze the task and provide structured guidance.
            """
    };

    private static string BuildUserPrompt(TaskMetadata metadata) => $"""
        Task Type: {metadata.TaskType}
        Target: {metadata.TargetFolder ?? "Not specified"}
        Context: {metadata.ContextPrompt ?? "None"}
        Expected Outcome: {metadata.ExpectedOutcome ?? "Successful completion"}
        Priority: {metadata.Priority}
        Timeout: {metadata.TimeoutSeconds} seconds
        """;

    private static string GenerateDefaultPlan(TaskMetadata metadata)
    {
        var target = metadata.TargetFolder ?? ".";
        var taskType = metadata.TaskType;

        return $$$"""
            {
                "task_type": "{{{taskType}}}",
                "steps": [
                    {"action": "validate_preconditions", "params": {}},
                    {"action": "execute_primary", "params": {"target": "{{{target}}}"}},
                    {"action": "validate_result", "params": {}}
                ],
                "fallback": "skip_on_error"
            }
            """;
    }

    #endregion
}
