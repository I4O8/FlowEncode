using FlowEncode.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class ReadinessStateResolverTests
{
    [TestMethod]
    public void ResolveFromRequirements_WhenAllRequirementsSatisfied_ReturnsReady()
    {
        var requirements = new[]
        {
            CreateRequirement(ReadinessState.Ready),
            CreateRequirement(ReadinessState.Ready)
        };

        var result = ReadinessStateResolver.ResolveFromRequirements(requirements);

        Assert.AreEqual(ReadinessState.Ready, result);
    }

    [TestMethod]
    public void ResolveFromRequirements_WhenSatisfiedAndMissingRequirementsAreMixed_ReturnsPartial()
    {
        var requirements = new[]
        {
            CreateRequirement(ReadinessState.Ready),
            CreateRequirement(ReadinessState.Missing)
        };

        var result = ReadinessStateResolver.ResolveFromRequirements(requirements);

        Assert.AreEqual(ReadinessState.Partial, result);
    }

    [TestMethod]
    public void ResolveFromRequirements_WhenUnsatisfiedRequirementIsMisconfigured_ReturnsMisconfigured()
    {
        var requirements = new[]
        {
            CreateRequirement(ReadinessState.Ready),
            CreateRequirement(ReadinessState.Misconfigured)
        };

        var result = ReadinessStateResolver.ResolveFromRequirements(requirements);

        Assert.AreEqual(ReadinessState.Misconfigured, result);
    }

    [TestMethod]
    public void ResolveFromRequirements_WhenNothingIsSatisfiedAndEverythingIsMissing_ReturnsMissing()
    {
        var requirements = new[]
        {
            CreateRequirement(ReadinessState.Missing),
            CreateRequirement(ReadinessState.Missing)
        };

        var result = ReadinessStateResolver.ResolveFromRequirements(requirements);

        Assert.AreEqual(ReadinessState.Missing, result);
    }

    [TestMethod]
    public void ResolveFromRequirements_WhenNothingIsSatisfiedAndEverythingIsUnknown_ReturnsUnknown()
    {
        var requirements = new[]
        {
            CreateRequirement(ReadinessState.Unknown),
            CreateRequirement(ReadinessState.Unknown)
        };

        var result = ReadinessStateResolver.ResolveFromRequirements(requirements);

        Assert.AreEqual(ReadinessState.Unknown, result);
    }

    private static CapabilityRequirementReadiness CreateRequirement(ReadinessState state)
    {
        return new CapabilityRequirementReadiness(
            new CapabilityToolRequirement(RegisteredToolKind.Ffmpeg),
            [new ToolProbeResult(
                RegisteredToolKind.Ffmpeg,
                state,
                ToolDetectionSource.None,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty)]);
    }
}
