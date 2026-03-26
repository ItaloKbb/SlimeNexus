namespace SlimeNexus.Core.Domain.Enums;

/// <summary>
/// Known pet states that affect task processing and rewards.
/// </summary>
public static class PetStates
{
    public const string Happy = "happy";
    public const string Sad = "sad";
    public const string Hungry = "hungry";
    public const string Tired = "tired";
    public const string Excited = "excited";
    public const string Bored = "bored";
    public const string Sick = "sick";
    public const string Sleeping = "sleeping";

    /// <summary>All known pet states.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Happy, Sad, Hungry, Tired, Excited, Bored, Sick, Sleeping
    ];

    /// <summary>
    /// Gets the XP multiplier based on pet state.
    /// Happy pets earn more XP, tired pets earn less.
    /// </summary>
    public static float GetXpMultiplier(string state) => state switch
    {
        Happy => 1.5f,
        Excited => 2.0f,
        Sad => 0.8f,
        Hungry => 0.9f,
        Tired => 0.7f,
        Bored => 1.0f,
        Sick => 0.5f,
        Sleeping => 0.0f, // Can't earn XP while sleeping
        _ => 1.0f
    };

    /// <summary>
    /// Checks if the pet can perform tasks in its current state.
    /// </summary>
    public static bool CanPerformTasks(string state) =>
        state is not (Sleeping or Sick);
}
