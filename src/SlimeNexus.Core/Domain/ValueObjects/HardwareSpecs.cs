namespace SlimeNexus.Core.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the hardware specifications of the local machine.
/// Used by the AI orchestrator to decide which model/strategy to employ.
/// </summary>
public readonly record struct HardwareSpecs
{
    /// <summary>GPU device name (e.g., "NVIDIA GeForce RTX 4070").</summary>
    public required string GpuName { get; init; }

    /// <summary>Total VRAM available on the GPU in megabytes.</summary>
    public required ulong VramTotalMb { get; init; }

    /// <summary>Currently available (free) VRAM in megabytes.</summary>
    public required ulong VramAvailableMb { get; init; }

    /// <summary>Total system RAM in megabytes.</summary>
    public required ulong RamTotalMb { get; init; }

    /// <summary>Available system RAM in megabytes.</summary>
    public required ulong RamAvailableMb { get; init; }

    /// <summary>Number of logical CPU cores.</summary>
    public required int CpuCoreCount { get; init; }

    /// <summary>CPU model name (e.g., "AMD Ryzen 9 5900X").</summary>
    public required string CpuName { get; init; }

    /// <summary>Indicates whether the GPU supports CUDA for AI inference.</summary>
    public bool SupportsCuda { get; init; }

    /// <summary>
    /// Returns true if the system has enough resources to run local LLM inference.
    /// Minimum: 8GB VRAM or 16GB RAM for CPU-only fallback.
    /// </summary>
    public bool CanRunLocalInference =>
        VramAvailableMb >= 8192 || (RamAvailableMb >= 16384 && CpuCoreCount >= 4);

    /// <summary>
    /// Suggests the optimal quantization level based on available VRAM.
    /// </summary>
    public string SuggestedQuantization => VramAvailableMb switch
    {
        >= 24576 => "fp16",      // 24GB+ VRAM: Full precision
        >= 12288 => "q8_0",      // 12GB+ VRAM: 8-bit quantization
        >= 8192 => "q4_k_m",     // 8GB+ VRAM: 4-bit medium
        >= 4096 => "q4_0",       // 4GB+ VRAM: 4-bit basic
        _ => "q2_k"              // Low VRAM: 2-bit (CPU fallback likely)
    };
}
