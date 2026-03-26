namespace SlimeNexus.Core.Domain.Entities;

/// <summary>
/// Represents a daily task that the user must complete to keep the Slime happy.
/// Tasks are validated by the SlimeNexus agent before being rewarded.
/// </summary>
public sealed class DailyTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime DueDate { get; set; }
    public int HappinessReward { get; set; } = 10;
    public int EnergyReward { get; set; } = 5;

    /// <summary>Marks the task as completed and records the timestamp.</summary>
    public void Complete()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
    }
}
