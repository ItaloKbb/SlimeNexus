namespace SlimeNexus.Core.Domain.Entities;

/// <summary>
/// Represents the Slime (Tamagotchi) entity — the core domain object of SlimeNexus.
/// A Slime has a lifecycle managed by daily tasks, AI interactions, and hardware conditions.
/// </summary>
public sealed class Slime
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Nexus";
    public int HappinessLevel { get; set; } = 100;   // 0–100
    public int EnergyLevel { get; set; } = 100;       // 0–100
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
    public bool IsAwake { get; set; } = true;

    /// <summary>Applies the result of a completed daily task to the Slime's stats.</summary>
    public void ApplyTaskReward(int happinessBonus, int energyBonus)
    {
        HappinessLevel = Math.Clamp(HappinessLevel + happinessBonus, 0, 100);
        EnergyLevel    = Math.Clamp(EnergyLevel    + energyBonus,    0, 100);
        LastInteraction = DateTime.UtcNow;
    }
}
