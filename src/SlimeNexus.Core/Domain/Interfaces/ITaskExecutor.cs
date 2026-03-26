using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Contract for task execution and validation.
/// Acts as a bridge to external tools (OpenClaw, file system, Git, etc.)
/// to validate that real-world tasks were completed.
/// Implemented in SlimeNexus.Infrastructure.Executors.
/// </summary>
public interface ITaskExecutor
{
    /// <summary>
    /// Gets the task types this executor can handle (e.g., "code_review", "git_commit").
    /// </summary>
    IReadOnlyList<string> SupportedTaskTypes { get; }

    /// <summary>
    /// Checks if this executor can handle the specified task type.
    /// </summary>
    /// <param name="taskType">The task type identifier.</param>
    /// <returns>True if this executor supports the task type.</returns>
    bool CanExecute(string taskType);

    /// <summary>
    /// Executes and validates a task based on the provided metadata.
    /// </summary>
    /// <param name="metadata">Task metadata from the frontend.</param>
    /// <param name="executionPlan">AI-generated execution plan (JSON).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Validation result with success status and rewards.</returns>
    Task<ValidationResult> ExecuteAsync(
        TaskMetadata metadata,
        string executionPlan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates preconditions before execution (e.g., folder exists, permissions OK).
    /// </summary>
    /// <param name="metadata">Task metadata to validate.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Tuple of (isValid, errorMessage). errorMessage is null if valid.</returns>
    Task<(bool IsValid, string? ErrorMessage)> ValidatePreconditionsAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default);
}
