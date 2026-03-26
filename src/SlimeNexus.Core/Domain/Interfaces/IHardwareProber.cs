using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Core.Domain.Interfaces;

/// <summary>
/// Contract for probing hardware specifications.
/// Implemented via WMI/LibreHardwareMonitor in SlimeNexus.Infrastructure.
/// Unlike IHardwareMonitor (real-time sensors), this provides static hardware info.
/// </summary>
public interface IHardwareProber
{
    /// <summary>
    /// Retrieves the current hardware specifications of the local machine.
    /// This is a relatively expensive operation; cache the result when possible.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Hardware specifications including GPU, CPU, and memory details.</returns>
    Task<HardwareSpecs> GetSpecsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a CUDA-compatible GPU is available for AI inference.
    /// </summary>
    bool HasCudaGpu();

    /// <summary>
    /// Gets the available VRAM in megabytes at the current moment.
    /// Lighter than GetSpecsAsync for quick checks.
    /// </summary>
    ulong GetAvailableVramMb();

    /// <summary>
    /// Gets the available system RAM in megabytes at the current moment.
    /// </summary>
    ulong GetAvailableRamMb();
}
