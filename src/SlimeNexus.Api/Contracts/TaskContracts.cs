using System.Text.Json.Serialization;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Api.Contracts;

/// <summary>
/// Request payload for task validation.
/// Maps to TaskMetadata domain model.
/// </summary>
public sealed record TaskMetadataRequest
{
    /// <summary>Current emotional state of the pet (e.g., "happy", "sad", "hungry").</summary>
    [JsonPropertyName("pet_state")]
    public required string PetState { get; init; }

    /// <summary>Type of task to validate (e.g., "code_review", "git_commit").</summary>
    [JsonPropertyName("task_type")]
    public required string TaskType { get; init; }

    /// <summary>Target folder for file-based operations.</summary>
    [JsonPropertyName("target_folder")]
    public string? TargetFolder { get; init; }

    /// <summary>Additional context or instructions for the AI.</summary>
    [JsonPropertyName("context_prompt")]
    public string? ContextPrompt { get; init; }

    /// <summary>Expected outcome or success criteria.</summary>
    [JsonPropertyName("expected_outcome")]
    public string? ExpectedOutcome { get; init; }

    /// <summary>Maximum time in seconds before timeout (1-600, default: 120).</summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>Priority level (1 = highest, 5 = lowest, default: 3).</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 3;

    /// <summary>XP reward multiplier based on task difficulty (default: 1.0).</summary>
    [JsonPropertyName("xp_multiplier")]
    public float XpMultiplier { get; init; } = 1.0f;

    /// <summary>Converts to domain model.</summary>
    public TaskMetadata ToDomain() => new()
    {
        RequestId = Guid.NewGuid(),
        PetState = PetState,
        TaskType = TaskType,
        TargetFolder = TargetFolder,
        ContextPrompt = ContextPrompt,
        ExpectedOutcome = ExpectedOutcome,
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 1, 600),
        Priority = Math.Clamp(Priority, 1, 5),
        XpMultiplier = XpMultiplier,
        CreatedAt = DateTimeOffset.UtcNow
    };
}

/// <summary>
/// Response for successful task validation.
/// </summary>
public sealed record ValidationResultResponse
{
    /// <summary>Request ID for correlation.</summary>
    [JsonPropertyName("request_id")]
    public required Guid RequestId { get; init; }

    /// <summary>Whether the task completed successfully.</summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>Human-readable result message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>XP points earned by the pet.</summary>
    [JsonPropertyName("xp_earned")]
    public int XpEarned { get; init; }

    /// <summary>Happiness bonus for the pet.</summary>
    [JsonPropertyName("happiness_bonus")]
    public int HappinessBonus { get; init; }

    /// <summary>Energy cost for the pet (usually negative).</summary>
    [JsonPropertyName("energy_cost")]
    public int EnergyCost { get; init; }

    /// <summary>Detailed output from execution (optional).</summary>
    [JsonPropertyName("detailed_output")]
    public string? DetailedOutput { get; init; }

    /// <summary>Error code if failed (optional).</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    /// <summary>Execution time in milliseconds.</summary>
    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; init; }

    /// <summary>Timestamp when completed.</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Creates from domain model.</summary>
    public static ValidationResultResponse FromDomain(ValidationResult result) => new()
    {
        RequestId = result.RequestId,
        Success = result.IsSuccess,
        Message = result.Message,
        XpEarned = result.XpEarned,
        HappinessBonus = result.HappinessBonus,
        EnergyCost = result.EnergyCost,
        DetailedOutput = result.DetailedOutput,
        ErrorCode = result.ErrorCode,
        ExecutionTimeMs = result.ExecutionTimeMs,
        CompletedAt = result.CompletedAt
    };
}

/// <summary>
/// Response when task is accepted for async processing.
/// </summary>
public sealed record TaskAcceptedResponse
{
    /// <summary>Request ID for tracking.</summary>
    [JsonPropertyName("request_id")]
    public required Guid RequestId { get; init; }

    /// <summary>Current status (queued, processing, completed, failed).</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Estimated duration in seconds.</summary>
    [JsonPropertyName("estimated_duration_seconds")]
    public int EstimatedDurationSeconds { get; init; }

    /// <summary>URL to check task status.</summary>
    [JsonPropertyName("status_url")]
    public required string StatusUrl { get; init; }
}

/// <summary>
/// Response for task status query.
/// </summary>
public sealed record TaskStatusResponse
{
    /// <summary>Request ID.</summary>
    [JsonPropertyName("request_id")]
    public required Guid RequestId { get; init; }

    /// <summary>Current status.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Progress percentage (0-100).</summary>
    [JsonPropertyName("progress")]
    public int Progress { get; init; }

    /// <summary>Validation result (available when status is "completed").</summary>
    [JsonPropertyName("result")]
    public ValidationResultResponse? Result { get; init; }

    /// <summary>Time queued.</summary>
    [JsonPropertyName("queued_at")]
    public DateTimeOffset QueuedAt { get; init; }

    /// <summary>Time started processing (if started).</summary>
    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Time completed (if completed).</summary>
    [JsonPropertyName("completed_at")]
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Response listing supported task types.
/// </summary>
public sealed record TaskTypesResponse
{
    [JsonPropertyName("types")]
    public required IReadOnlyList<TaskTypeInfo> Types { get; init; }
}

/// <summary>
/// Information about a task type.
/// </summary>
public sealed record TaskTypeInfo
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("estimated_duration_seconds")]
    public int EstimatedDurationSeconds { get; init; }
}
