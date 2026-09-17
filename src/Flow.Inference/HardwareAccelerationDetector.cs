using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Microsoft.Win32;

namespace Flow.Inference;

public enum HardwareAccelerationType
{
    DirectML_GPU,
    CUDA_GPU,
    NPU_DirectML,
    CPU_AVX512,
    CPU_AVX2,
    CPU_Standard
}

public sealed record HardwareProfile(
    string PrimaryGpuName,
    long DedicatedVramMB,
    bool IsDiscreteGpu,
    bool DirectMLSupported,
    HardwareAccelerationType RecommendedBackend,
    int OptimalCpuThreads,
    string CpuArchitecture,
    IReadOnlyList<string> AllDetectedGpus
);

/// <summary>
/// Probes Windows graphics display adapters, DXGI capabilities, and CPU SIMD intrinsics
/// to determine optimal Whisper neural inference backend and thread allocation.
/// </summary>
public static class HardwareAccelerationDetector
{
    public static HardwareProfile Detect()
    {
        // 1. Detect CPU capabilities & optimal threads
        int logicalCores = Environment.ProcessorCount;
        int optimalThreads = Math.Max(2, Math.Min(8, logicalCores / 2));
        string cpuArch = RuntimeInformation.ProcessArchitecture.ToString();

        HardwareAccelerationType cpuFallback = HardwareAccelerationType.CPU_Standard;
        if (Avx512F.IsSupported)
            cpuFallback = HardwareAccelerationType.CPU_AVX512;
        else if (Avx2.IsSupported)
            cpuFallback = HardwareAccelerationType.CPU_AVX2;

        // 2. Detect Display Adapters (GPUs)
        var detectedGpus = new List<string>();
        string primaryGpu = "Standard Windows Display Adapter";
        long maxVramBytes = 0;
        bool isDiscrete = false;
        bool hasDirectMLSupport = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (classKey != null)
            {
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    if (subKeyName.Length == 4 && int.TryParse(subKeyName, out _))
                    {
                        using var subKey = classKey.OpenSubKey(subKeyName);
                        var driverDesc = subKey?.GetValue("DriverDesc") as string;
                        if (!string.IsNullOrWhiteSpace(driverDesc))
                        {
                            detectedGpus.Add(driverDesc);

                            // Check VRAM size
                            long vram = 0;
                            var qwMem = subKey?.GetValue("HardwareInformation.qwMemorySize");
                            if (qwMem is long l) vram = l;
                            else if (qwMem is int i) vram = (uint)i;
                            else
                            {
                                var ram = subKey?.GetValue("HardwareInformation.MemorySize");
                                if (ram is byte[] b && b.Length >= 4)
                                    vram = BitConverter.ToUInt32(b, 0);
                            }

                            bool isNvidia = driverDesc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || 
                                           driverDesc.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || 
                                           driverDesc.Contains("RTX", StringComparison.OrdinalIgnoreCase);
                            bool isAmd = driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase) || 
                                        driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase);
                            bool isIntelArc = driverDesc.Contains("Intel", StringComparison.OrdinalIgnoreCase) && 
                                             driverDesc.Contains("Arc", StringComparison.OrdinalIgnoreCase);

                            if (isNvidia || isAmd || isIntelArc || vram > maxVramBytes)
                            {
                                primaryGpu = driverDesc;
                                maxVramBytes = Math.Max(maxVramBytes, vram);
                                if (isNvidia || isAmd || isIntelArc || vram >= 1024L * 1024 * 1024)
                                {
                                    isDiscrete = true;
                                }
                            }
                        }
                    }
                }
            }
            }
        }
        catch
        {
            // Fallback gracefully on non-admin or restricted permissions
        }

        if (detectedGpus.Count == 0)
        {
            detectedGpus.Add(primaryGpu);
        }

        long vramMB = maxVramBytes / (1024 * 1024);

        // 3. Determine recommended inference backend
        HardwareAccelerationType recommended = cpuFallback;
        if (hasDirectMLSupport && (isDiscrete || vramMB >= 1024))
        {
            recommended = HardwareAccelerationType.DirectML_GPU;
        }
        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            recommended = HardwareAccelerationType.NPU_DirectML;
        }

        return new HardwareProfile(
            PrimaryGpuName: primaryGpu,
            DedicatedVramMB: vramMB,
            IsDiscreteGpu: isDiscrete,
            DirectMLSupported: hasDirectMLSupport,
            RecommendedBackend: recommended,
            OptimalCpuThreads: optimalThreads,
            CpuArchitecture: cpuArch,
            AllDetectedGpus: detectedGpus
        );
    }
}
