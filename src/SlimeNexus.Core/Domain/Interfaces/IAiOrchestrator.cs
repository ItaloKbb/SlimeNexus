using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Contract for the AI orchestration layer - the "brain" of SlimeNexus.
/// Decides which AI model/tool to use based on task type and hardware capabilities.
/// Implemented in SlimeNexus.Infrastructure.AI.
/// </summary>
public interface IAiOrchestrator
{
    /// <summary>
    /// Processes a task request and determines the best execution strategy.
    /// Orchestrates between local Ollama models and external tools like OpenClaw.
    /// </summary>
    /// <param name="metadata">Task metadata from the frontend.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Validation result with XP rewards and status.</returns>
    Task<ValidationResult> ProcessTaskAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the orchestrator is ready to process tasks.
    /// Verifies AI services (Ollama) are running and accessible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if all required services are available.</returns>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the currently selected AI model.
    /// </summary>
    string CurrentModelName { get; }

    /// <summary>
    /// Gets the recommended model based on current hardware capabilities.
    /// </summary>
    /// <param name="specs">Hardware specifications to evaluate.</param>
    /// <returns>Model identifier (e.g., "llama3:8b-instruct-q4_K_M").</returns>
    string GetRecommendedModel(HardwareSpecs specs);

    /// <summary>
    /// Analyzes task metadata and generates an execution plan.
    /// </summary>
    /// <param name="metadata">Task metadata to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Structured execution plan as JSON string.</returns>
    Task<string> GenerateExecutionPlanAsync(
        TaskMetadata metadata,
        CancellationToken cancellationToken = default);
}
