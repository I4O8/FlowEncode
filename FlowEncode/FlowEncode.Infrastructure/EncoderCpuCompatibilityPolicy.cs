using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace FlowEncode.Infrastructure;

internal static class EncoderCpuCompatibilityPolicy
{
    public static EncoderCpuProfile DetectCurrent()
    {
        try
        {
            if (!Environment.Is64BitProcess || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                return EncoderCpuProfile.Unknown("当前进程不是 x64。");
            }

            if (!X86Base.IsSupported)
            {
                return EncoderCpuProfile.Unknown("当前运行时未公开 x86 CPUID 能力。");
            }

            var (_, _, leaf1Ecx, _) = X86Base.CpuId(1, 0);
            var ssse3 = Ssse3.IsSupported;
            var sse41 = Sse41.IsSupported;
            var sse42 = Sse42.IsSupported;
            var popcnt = Popcnt.IsSupported;
            var avx = Avx.IsSupported;
            var avx2 = Avx2.IsSupported;
            var fma = Fma.IsSupported;
            var f16c = IsBitSet(leaf1Ecx, 29);
            var bmi1 = Bmi1.IsSupported;
            var bmi2 = Bmi2.IsSupported;
            var lzcnt = Lzcnt.IsSupported;
            var movbe = IsBitSet(leaf1Ecx, 22);
            var avx512 = Avx512F.IsSupported
                && Avx512BW.IsSupported
                && Avx512CD.IsSupported
                && Avx512DQ.IsSupported;

            return new EncoderCpuProfile(
                DetectionSucceeded: true,
                FailureReason: string.Empty,
                SupportsSsse3: ssse3,
                SupportsSse41: sse41,
                SupportsSse42: sse42,
                SupportsPopcnt: popcnt,
                SupportsAvx: avx,
                SupportsAvx2: avx2,
                SupportsFma: fma,
                SupportsF16c: f16c,
                SupportsBmi1: bmi1,
                SupportsBmi2: bmi2,
                SupportsLzcnt: lzcnt,
                SupportsMovbe: movbe,
                SupportsAvx512: avx512);
        }
        catch (Exception exception)
        {
            return EncoderCpuProfile.Unknown(exception.Message);
        }
    }

    public static EncoderAssetCompatibility Evaluate(string assetName, EncoderCpuProfile cpuProfile)
    {
        var tier = Classify(assetName);
        if (!cpuProfile.DetectionSucceeded)
        {
            return tier switch
            {
                EncoderAssetTier.X86_64Generic => Compatible(tier, 300, "CPU 能力检测失败，仅允许通用 x86-64 构建。"),
                EncoderAssetTier.X64Generic => Compatible(tier, 280, "CPU 能力检测失败，仅允许通用 x64 构建。"),
                _ => Incompatible(tier, "CPU 能力检测失败，已拒绝非通用构建。")
            };
        }

        return tier switch
        {
            EncoderAssetTier.Avx512 when cpuProfile.SupportsAvx512Tier =>
                Compatible(tier, 520, "CPU 支持 AVX-512，允许选择 AVX-512 构建。"),
            EncoderAssetTier.X86_64V3 when cpuProfile.SupportsX86_64V3Tier =>
                Compatible(tier, 470, "CPU 支持 x86-64-v3 基线，允许选择 v3 构建。"),
            EncoderAssetTier.Avx2 when cpuProfile.SupportsAvx2Tier =>
                Compatible(tier, 440, "CPU 支持 AVX2，允许选择 AVX2 构建。"),
            EncoderAssetTier.Avx when cpuProfile.SupportsAvxTier =>
                Compatible(tier, 410, "CPU 支持 AVX，允许选择 AVX 构建。"),
            EncoderAssetTier.X86_64Generic =>
                Compatible(tier, 300, "始终允许通用 x86-64 构建。"),
            EncoderAssetTier.X64Generic =>
                Compatible(tier, 280, "始终允许通用 x64 构建。"),
            EncoderAssetTier.MicroarchitectureSpecific =>
                Incompatible(tier, "已拒绝微架构特化构建，避免把用户装到错误 CPU 目标上。"),
            EncoderAssetTier.Unknown =>
                Incompatible(tier, "无法识别该构建的 CPU 目标，已拒绝自动选择。"),
            EncoderAssetTier.Avx512 =>
                Incompatible(tier, "CPU 不支持 AVX-512。"),
            EncoderAssetTier.X86_64V3 =>
                Incompatible(tier, "CPU 不满足 x86-64-v3 基线。"),
            EncoderAssetTier.Avx2 =>
                Incompatible(tier, "CPU 不支持 AVX2。"),
            EncoderAssetTier.Avx =>
                Incompatible(tier, "CPU 不支持 AVX。"),
            _ =>
                Incompatible(tier, "当前 CPU 不兼容该构建。")
        };
    }

    public static string BuildSelectionNote(EncoderCpuProfile cpuProfile, EncoderAssetCompatibility compatibility)
    {
        if (!cpuProfile.DetectionSucceeded)
        {
            return $"CPU 检测失败（{cpuProfile.FailureReason}），已仅允许通用构建。";
        }

        return compatibility.Tier switch
        {
            EncoderAssetTier.Avx512 => "CPU 检测通过，已选择兼容的 AVX-512 构建。",
            EncoderAssetTier.X86_64V3 => "CPU 检测通过，已选择兼容的 x86-64-v3 构建。",
            EncoderAssetTier.Avx2 => "CPU 检测通过，已选择兼容的 AVX2 构建。",
            EncoderAssetTier.Avx => "CPU 检测通过，已选择兼容的 AVX 构建。",
            EncoderAssetTier.X86_64Generic or EncoderAssetTier.X64Generic =>
                "CPU 检测通过，已回退到最保守的通用构建。",
            _ => "CPU 检测通过，已仅在兼容资产中完成筛选。"
        };
    }

    private static EncoderAssetCompatibility Compatible(EncoderAssetTier tier, int preference, string reason)
    {
        return new EncoderAssetCompatibility(true, tier, preference, reason);
    }

    private static EncoderAssetCompatibility Incompatible(EncoderAssetTier tier, string reason)
    {
        return new EncoderAssetCompatibility(false, tier, int.MinValue, reason);
    }

    private static EncoderAssetTier Classify(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return EncoderAssetTier.Unknown;
        }

        if (assetName.Contains("avx512", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.Avx512;
        }

        if (assetName.Contains("x86-64-v3", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.X86_64V3;
        }

        if (assetName.Contains("avx2", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.Avx2;
        }

        if (assetName.Contains("avx", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.Avx;
        }

        if (assetName.Contains("alderlake", StringComparison.OrdinalIgnoreCase)
            || assetName.Contains("znver", StringComparison.OrdinalIgnoreCase)
            || assetName.Contains("haswell", StringComparison.OrdinalIgnoreCase)
            || assetName.Contains("skylake", StringComparison.OrdinalIgnoreCase)
            || assetName.Contains("sandybridge", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.MicroarchitectureSpecific;
        }

        if (assetName.Contains("x86-64", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.X86_64Generic;
        }

        if (assetName.Contains("x64", StringComparison.OrdinalIgnoreCase))
        {
            return EncoderAssetTier.X64Generic;
        }

        return EncoderAssetTier.Unknown;
    }

    private static bool IsBitSet(int value, int bitIndex)
    {
        return ((value >> bitIndex) & 1) != 0;
    }
}

internal sealed record EncoderCpuProfile(
    bool DetectionSucceeded,
    string FailureReason,
    bool SupportsSsse3,
    bool SupportsSse41,
    bool SupportsSse42,
    bool SupportsPopcnt,
    bool SupportsAvx,
    bool SupportsAvx2,
    bool SupportsFma,
    bool SupportsF16c,
    bool SupportsBmi1,
    bool SupportsBmi2,
    bool SupportsLzcnt,
    bool SupportsMovbe,
    bool SupportsAvx512)
{
    public bool SupportsAvxTier => SupportsSsse3 && SupportsSse41 && SupportsSse42 && SupportsPopcnt && SupportsAvx;

    public bool SupportsAvx2Tier => SupportsAvxTier && SupportsAvx2;

    public bool SupportsX86_64V3Tier =>
        SupportsAvx2Tier
        && SupportsFma
        && SupportsF16c
        && SupportsBmi1
        && SupportsBmi2
        && SupportsLzcnt
        && SupportsMovbe;

    public bool SupportsAvx512Tier => SupportsX86_64V3Tier && SupportsAvx512;

    public static EncoderCpuProfile Unknown(string failureReason)
    {
        return new EncoderCpuProfile(
            DetectionSucceeded: false,
            FailureReason: string.IsNullOrWhiteSpace(failureReason) ? "unknown" : failureReason,
            SupportsSsse3: false,
            SupportsSse41: false,
            SupportsSse42: false,
            SupportsPopcnt: false,
            SupportsAvx: false,
            SupportsAvx2: false,
            SupportsFma: false,
            SupportsF16c: false,
            SupportsBmi1: false,
            SupportsBmi2: false,
            SupportsLzcnt: false,
            SupportsMovbe: false,
            SupportsAvx512: false);
    }
}

internal sealed record EncoderAssetCompatibility(
    bool IsCompatible,
    EncoderAssetTier Tier,
    int Preference,
    string Reason);

internal enum EncoderAssetTier
{
    Unknown = 0,
    X64Generic = 1,
    X86_64Generic = 2,
    Avx = 3,
    Avx2 = 4,
    X86_64V3 = 5,
    Avx512 = 6,
    MicroarchitectureSpecific = 7
}
