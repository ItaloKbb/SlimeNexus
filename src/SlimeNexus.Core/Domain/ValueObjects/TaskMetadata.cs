namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the payload received from the frontend web app.
/// Contains all context needed for the SlimeNexus agent to orchestrate task validation.
/// </summary>
public sealed record TaskMetadata
{
    /// <summary>Unique identifier for this task request.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>Current emotional state of the pet (e.g., "happy", "sad", "hungry", "tired").</summary>
    public required string PetState { get; init; }

    /// <summary>Type of task to validate (e.g., "code_review", "file_cleanup", "git_commit", "test_run").</summary>
    public required string TaskType { get; init; }

    /// <summary>Target path for file-based operations (optional).</summary>
    public string? TargetFolder { get; init; }

    /// <summary>Additional context or instructions for the AI (optional).</summary>
    public string? ContextPrompt { get; init; }

    /// <summary>Expected outcome or success criteria (optional).</summary>
    public string? ExpectedOutcome { get; init; }

    /// <summary>Maximum time in seconds the task should take before timing out.</summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Priority level (1 = highest, 5 = lowest).</summary>
    public int Priority { get; init; } = 3;

    /// <summary>Timestamp when the request was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>XP reward multiplier based on task difficulty (1.0 = normal).</summary>
    public float XpMultiplier { get; init; } = 1.0f;

    /// <summary>Validates that required fields are properly set.</summary>
    public bool IsValid =>
        RequestId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(PetState) &&
        !string.IsNullOrWhiteSpace(TaskType) &&
        TimeoutSeconds > 0 &&
        Priority is >= 1 and <= 5;
}
