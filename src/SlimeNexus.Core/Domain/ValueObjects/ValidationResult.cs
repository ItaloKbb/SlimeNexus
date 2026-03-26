namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the outcome of a task validation.
/// Returned to the frontend with XP rewards and status details.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>Correlates with the original TaskMetadata.RequestId.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>True if the task was completed successfully.</summary>
    public required bool IsSuccess { get; init; }

    /// <summary>Human-readable summary of the validation outcome.</summary>
    public required string Message { get; init; }

    /// <summary>XP points earned by the pet (0 if failed).</summary>
    public int XpEarned { get; init; }

    /// <summary>Happiness points to add to the pet.</summary>
    public int HappinessBonus { get; init; }

    /// <summary>Energy cost for the pet (usually negative).</summary>
    public int EnergyCost { get; init; }

    /// <summary>Detailed output from the AI or executor (for debugging/display).</summary>
    public string? DetailedOutput { get; init; }

    /// <summary>Timestamp when validation completed.</summary>
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Time taken to execute the validation in milliseconds.</summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>Error code if validation failed (null on success).</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Creates a successful validation result.</summary>
    public static ValidationResult Success(
        Guid requestId,
        string message,
        int xpEarned,
        int happinessBonus = 10,
        int energyCost = -5,
        string? detailedOutput = null,
        long executionTimeMs = 0) => new()
    {
        RequestId = requestId,
        IsSuccess = true,
        Message = message,
        XpEarned = xpEarned,
        HappinessBonus = happinessBonus,
        EnergyCost = energyCost,
        DetailedOutput = detailedOutput,
        CompletedAt = DateTimeOffset.UtcNow,
        ExecutionTimeMs = executionTimeMs
    };

    /// <summary>Creates a failed validation result.</summary>
    public static ValidationResult Failure(
        Guid requestId,
        string message,
        string errorCode,
        string? detailedOutput = null,
        long executionTimeMs = 0) => new()
    {
        RequestId = requestId,
        IsSuccess = false,
        Message = message,
        XpEarned = 0,
        HappinessBonus = 0,
        EnergyCost = -2, // Small penalty for failed attempts
        DetailedOutput = detailedOutput,
        ErrorCode = errorCode,
        CompletedAt = DateTimeOffset.UtcNow,
        ExecutionTimeMs = executionTimeMs
    };
}
