using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class EncoderCpuCompatibilityPolicyTests
{
    [TestMethod]
    public void Evaluate_WhenCpuDetectionFails_AllowsOnlyGenericPackages()
    {
        var unknownCpu = EncoderCpuProfile.Unknown("probe failed");

        var genericResult = EncoderCpuCompatibilityPolicy.Evaluate(
            "x265-4.2-.Mod-by-Patman.-x64-x86-64-gcc15.2.0.7z",
            unknownCpu);
        var avx2Result = EncoderCpuCompatibilityPolicy.Evaluate(
            "x265-4.2-.Mod-by-Patman.-x64-avx2-msvc19.44.7z",
            unknownCpu);

        Assert.IsTrue(genericResult.IsCompatible);
        Assert.IsFalse(avx2Result.IsCompatible);
    }

    [TestMethod]
    public void Evaluate_WhenCpuSupportsX8664V3_AllowsV3Packages()
    {
        var capableCpu = new EncoderCpuProfile(
            DetectionSucceeded: true,
            FailureReason: string.Empty,
            SupportsSsse3: true,
            SupportsSse41: true,
            SupportsSse42: true,
            SupportsPopcnt: true,
            SupportsAvx: true,
            SupportsAvx2: true,
            SupportsFma: true,
            SupportsF16c: true,
            SupportsBmi1: true,
            SupportsBmi2: true,
            SupportsLzcnt: true,
            SupportsMovbe: true,
            SupportsAvx512: false);

        var result = EncoderCpuCompatibilityPolicy.Evaluate(
            "x265-4.2-.Mod-by-Patman.-x64-x86-64-v3-gcc15.2.0.7z",
            capableCpu);

        Assert.IsTrue(result.IsCompatible);
        Assert.AreEqual(EncoderAssetTier.X86_64V3, result.Tier);
    }

    [TestMethod]
    public void Evaluate_RejectsMicroarchitectureSpecificPackages()
    {
        var capableCpu = new EncoderCpuProfile(
            DetectionSucceeded: true,
            FailureReason: string.Empty,
            SupportsSsse3: true,
            SupportsSse41: true,
            SupportsSse42: true,
            SupportsPopcnt: true,
            SupportsAvx: true,
            SupportsAvx2: true,
            SupportsFma: true,
            SupportsF16c: true,
            SupportsBmi1: true,
            SupportsBmi2: true,
            SupportsLzcnt: true,
            SupportsMovbe: true,
            SupportsAvx512: true);

        var result = EncoderCpuCompatibilityPolicy.Evaluate(
            "x265-4.2-.Mod-by-Patman.-x64-znver4-gcc15.2.0.7z",
            capableCpu);

        Assert.IsFalse(result.IsCompatible);
        Assert.AreEqual(EncoderAssetTier.MicroarchitectureSpecific, result.Tier);
    }
}
