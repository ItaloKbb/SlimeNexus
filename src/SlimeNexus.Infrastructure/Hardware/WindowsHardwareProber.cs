using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using SlimeNexus.Core.Domain.Interfaces;
using SlimeNexus.Core.Domain.ValueObjects;

namespace SlimeNexus.Infrastructure.Hardware;

/// <summary>
/// Windows-specific hardware probing implementation using WMI.
/// Optimized for detecting AMD GPUs (RDNA 2/3 architecture like RX 6750 XT).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareProber : IHardwareProber, IDisposable
{
    private readonly ILogger<WindowsHardwareProber> _logger;
    private readonly object _cacheLock = new();
    
    private HardwareSpecs? _cachedSpecs;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Known AMD GPU identifiers for RDNA 2/3 architecture
    private static readonly HashSet<string> AmdGpuIdentifiers =
    [
        "AMD", "ATI", "Radeon", "RDNA"
    ];

    // Known NVIDIA GPU identifiers for CUDA detection
    private static readonly HashSet<string> NvidiaGpuIdentifiers =
    [
        "NVIDIA", "GeForce", "RTX", "GTX", "Quadro", "Tesla"
    ];

    public WindowsHardwareProber(ILogger<WindowsHardwareProber> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HardwareSpecs> GetSpecsAsync(CancellationToken cancellationToken = default)
    {
        lock (_cacheLock)
        {
            if (_cachedSpecs is not null && DateTime.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached hardware specs");
                return _cachedSpecs.Value;
            }
        }

        // Run WMI queries on thread pool to avoid blocking
        var specs = await Task.Run(() => ProbeHardwareSpecs(), cancellationToken);

        lock (_cacheLock)
        {
            _cachedSpecs = specs;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
        }

        return specs;
    }

    /// <inheritdoc />
    public bool HasCudaGpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");

            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? string.Empty;
                if (NvidiaGpuIdentifiers.Any(id => 
                    name.Contains(id, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogDebug("CUDA-capable GPU detected: {GpuName}", name);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect CUDA GPU via WMI");
        }

        return false;
    }

    /// <inheritdoc />
    public ulong GetAvailableVramMb()
    {
        try
        {
            // For AMD GPUs, we use AdapterRAM from Win32_VideoController
            // Note: WMI reports total VRAM, not available. For accurate available VRAM,
            // we'd need DirectX or vendor-specific APIs (ADL for AMD, NVML for NVIDIA)
            using var searcher = new ManagementObjectSearcher(
                "SELECT AdapterRAM FROM Win32_VideoController WHERE AdapterRAM > 0");

            ulong maxVram = 0;
            foreach (var obj in searcher.Get())
            {
                if (obj["AdapterRAM"] is uint vram)
                {
                    // WMI returns bytes, convert to MB
                    var vramMb = vram / (1024UL * 1024UL);
                    maxVram = Math.Max(maxVram, vramMb);
                }
            }

            // Estimate 80% available when system is idle
            return (ulong)(maxVram * 0.8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get VRAM info via WMI");
            return 0;
        }
    }

    /// <inheritdoc />
    public ulong GetAvailableRamMb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (var obj in searcher.Get())
            {
                if (obj["FreePhysicalMemory"] is ulong freeKb)
                {
                    return freeKb / 1024UL; // Convert KB to MB
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get available RAM via WMI");
        }

        return 0;
    }

    private HardwareSpecs ProbeHardwareSpecs()
    {
        _logger.LogInformation("Probing hardware specifications via WMI...");

        var gpuInfo = GetGpuInfo();
        var cpuInfo = GetCpuInfo();
        var ramInfo = GetRamInfo();

        var specs = new HardwareSpecs
        {
            GpuName = gpuInfo.Name,
            VramTotalMb = gpuInfo.VramTotalMb,
            VramAvailableMb = gpuInfo.VramAvailableMb,
            SupportsCuda = gpuInfo.SupportsCuda,
            CpuName = cpuInfo.Name,
            CpuCoreCount = cpuInfo.CoreCount,
            RamTotalMb = ramInfo.TotalMb,
            RamAvailableMb = ramInfo.AvailableMb
        };

        _logger.LogInformation(
            "Hardware probed: GPU={Gpu} ({Vram}MB), CPU={Cpu} ({Cores} cores), RAM={Ram}MB",
            specs.GpuName, specs.VramTotalMb, specs.CpuName, specs.CpuCoreCount, specs.RamTotalMb);

        return specs;
    }

    private (string Name, ulong VramTotalMb, ulong VramAvailableMb, bool SupportsCuda) GetGpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, VideoProcessor FROM Win32_VideoController");

            string bestGpuName = "Unknown GPU";
            ulong bestVram = 0;
            bool supportsCuda = false;

            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown";
                var videoProcessor = obj["VideoProcessor"]?.ToString() ?? "";
                
                // AdapterRAM is uint32 in WMI, which caps at 4GB
                // For modern GPUs with >4GB, we need to use different detection
                ulong vramMb = 0;
                if (obj["AdapterRAM"] is uint adapterRam && adapterRam > 0)
                {
                    vramMb = adapterRam / (1024UL * 1024UL);
                }

                // Detect AMD RX 6000 series (RDNA 2) VRAM sizes
                vramMb = EstimateVramForKnownGpus(name, vramMb);

                // Track the GPU with most VRAM (likely discrete GPU)
                if (vramMb >= bestVram)
                {
                    bestVram = vramMb;
                    bestGpuName = name;
                }

                // Check for CUDA support
                if (NvidiaGpuIdentifiers.Any(id => 
                    name.Contains(id, StringComparison.OrdinalIgnoreCase)))
                {
                    supportsCuda = true;
                }

                _logger.LogDebug("Detected GPU: {Name}, VRAM: {Vram}MB, Processor: {Processor}",
                    name, vramMb, videoProcessor);
            }

            // Estimate available VRAM as 85% of total when system is idle
            var availableVram = (ulong)(bestVram * 0.85);

            return (bestGpuName, bestVram, availableVram, supportsCuda);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query GPU information");
            return ("Unknown GPU", 0, 0, false);
        }
    }

    /// <summary>
    /// Estimates VRAM for known GPU models when WMI reports incorrect values.
    /// WMI's AdapterRAM is a 32-bit value, so GPUs with >4GB report truncated values.
    /// </summary>
    private static ulong EstimateVramForKnownGpus(string gpuName, ulong reportedVram)
    {
        var upperName = gpuName.ToUpperInvariant();

        // AMD RX 6000 Series (RDNA 2)
        if (upperName.Contains("RX 6750 XT")) return 12288; // 12GB
        if (upperName.Contains("RX 6700 XT")) return 12288; // 12GB
        if (upperName.Contains("RX 6800 XT")) return 16384; // 16GB
        if (upperName.Contains("RX 6800")) return 16384;    // 16GB
        if (upperName.Contains("RX 6900 XT")) return 16384; // 16GB
        if (upperName.Contains("RX 6950 XT")) return 16384; // 16GB
        if (upperName.Contains("RX 6600 XT")) return 8192;  // 8GB
        if (upperName.Contains("RX 6600")) return 8192;     // 8GB
        if (upperName.Contains("RX 6500 XT")) return 4096;  // 4GB
        if (upperName.Contains("RX 6400")) return 4096;     // 4GB

        // AMD RX 7000 Series (RDNA 3)
        if (upperName.Contains("RX 7900 XTX")) return 24576; // 24GB
        if (upperName.Contains("RX 7900 XT")) return 20480;  // 20GB
        if (upperName.Contains("RX 7900 GRE")) return 16384; // 16GB
        if (upperName.Contains("RX 7800 XT")) return 16384;  // 16GB
        if (upperName.Contains("RX 7700 XT")) return 12288;  // 12GB
        if (upperName.Contains("RX 7600")) return 8192;      // 8GB

        // NVIDIA RTX 40 Series
        if (upperName.Contains("RTX 4090")) return 24576;   // 24GB
        if (upperName.Contains("RTX 4080 SUPER")) return 16384; // 16GB
        if (upperName.Contains("RTX 4080")) return 16384;   // 16GB
        if (upperName.Contains("RTX 4070 TI SUPER")) return 16384; // 16GB
        if (upperName.Contains("RTX 4070 TI")) return 12288; // 12GB
        if (upperName.Contains("RTX 4070 SUPER")) return 12288; // 12GB
        if (upperName.Contains("RTX 4070")) return 12288;   // 12GB
        if (upperName.Contains("RTX 4060 TI")) return 8192; // 8GB
        if (upperName.Contains("RTX 4060")) return 8192;    // 8GB

        // NVIDIA RTX 30 Series
        if (upperName.Contains("RTX 3090")) return 24576;   // 24GB
        if (upperName.Contains("RTX 3080 TI")) return 12288; // 12GB
        if (upperName.Contains("RTX 3080")) return 10240;   // 10GB
        if (upperName.Contains("RTX 3070 TI")) return 8192; // 8GB
        if (upperName.Contains("RTX 3070")) return 8192;    // 8GB
        if (upperName.Contains("RTX 3060 TI")) return 8192; // 8GB
        if (upperName.Contains("RTX 3060")) return 12288;   // 12GB

        // If no match, return the WMI-reported value (might be wrong for >4GB)
        // If reported is 0 or suspiciously low, assume 4GB as minimum discrete GPU
        return reportedVram > 1024 ? reportedVram : 4096;
    }

    private (string Name, int CoreCount) GetCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor");

            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                var cores = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? Environment.ProcessorCount);
                return (name, cores);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query CPU information");
        }

        return ("Unknown CPU", Environment.ProcessorCount);
    }

    private (ulong TotalMb, ulong AvailableMb) GetRamInfo()
    {
        try
        {
            using var osSearcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

            foreach (var obj in osSearcher.Get())
            {
                var totalKb = Convert.ToUInt64(obj["TotalVisibleMemorySize"] ?? 0UL);
                var freeKb = Convert.ToUInt64(obj["FreePhysicalMemory"] ?? 0UL);
                return (totalKb / 1024UL, freeKb / 1024UL);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query RAM information");
        }

        return (0, 0);
    }

    public void Dispose()
    {
        // Clear cached data
        lock (_cacheLock)
        {
            _cachedSpecs = null;
        }
    }
}
