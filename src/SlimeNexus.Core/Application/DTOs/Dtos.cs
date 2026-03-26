namespace SlimeNexus.Core.Application.DTOs;

/// <summary>Data Transfer Object representing the current Slime status.</summary>
public sealed record SlimeStatusDto(
    Guid Id,
    string Name,
    int HappinessLevel,
    int EnergyLevel,
    bool IsAwake,
    DateTime LastInteraction
);

/// <summary>Data Transfer Object representing a daily task summary.</summary>
public sealed record DailyTaskDto(
    Guid Id,
    string Title,
    string Description,
    bool IsCompleted,
    DateTime DueDate,
    int HappinessReward,
    int EnergyReward
);

/// <summary>Data Transfer Object for hardware sensor readings.</summary>
public sealed record HardwareStatusDto(
    float? GpuTemperatureCelsius,
    float? GpuLoadPercent,
    float? GpuVramUsedMb,
    float? CpuTemperatureCelsius
);
