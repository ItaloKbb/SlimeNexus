namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Contract for hardware monitoring services (GPU, CPU, RAM).
/// Implemented via LibreHardwareMonitor in SlimeNexus.Infrastructure.Hardware.
/// </summary>
public interface IHardwareMonitor
{
    /// <summary>GPU temperature in °C, or null if unavailable.</summary>
    float? GpuTemperatureCelsius { get; }

    /// <summary>GPU load percentage (0–100), or null if unavailable.</summary>
    float? GpuLoadPercent { get; }

    /// <summary>GPU VRAM used in MB, or null if unavailable.</summary>
    float? GpuVramUsedMb { get; }

    /// <summary>CPU temperature in °C, or null if unavailable.</summary>
    float? CpuTemperatureCelsius { get; }

    /// <summary>Refreshes all hardware sensor readings.</summary>
    void Update();
}
