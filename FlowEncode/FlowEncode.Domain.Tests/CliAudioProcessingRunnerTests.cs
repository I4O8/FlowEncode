using FlowEncode.Application;
using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class CliAudioProcessingRunnerTests
{
    [TestMethod]
    public void BuildDisplayCommand_WhenOpusUsesDefaultPipeline_UsesOpusencPipe()
    {
        var runner = new CliAudioProcessingRunner(new StubToolProbeService());
        var request = CreateRequest(useOpusMappingFamily1: false);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "ffmpeg.exe");
        StringAssert.Contains(command, "opusenc.exe");
        StringAssert.Contains(command, "-f wav - |");
        Assert.IsFalse(command.Contains("--quiet", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("-loglevel error", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("-mapping_family 1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildDisplayCommand_WhenOpusMappingFamilyIsEnabled_UsesFfmpegLibopus()
    {
        var runner = new CliAudioProcessingRunner(new StubToolProbeService());
        var request = CreateRequest(useOpusMappingFamily1: true);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "ffmpeg.exe");
        StringAssert.Contains(command, "-c:a libopus");
        StringAssert.Contains(command, "-mapping_family 1");
        Assert.IsFalse(command.Contains("-loglevel error", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("opusenc.exe", StringComparison.Ordinal));
    }

    private static AudioProcessingRequest CreateRequest(bool useOpusMappingFamily1)
    {
        return new AudioProcessingRequest(
            Guid.NewGuid(),
            @"D:\audio\input.thd",
            @"D:\audio\output.opus",
            AudioProcessingMode.Opus,
            null,
            [],
            120d,
            384,
            useOpusMappingFamily1);
    }

    private sealed class StubToolProbeService : IToolProbeService
    {
        public Task<IReadOnlyList<ToolProbeResult>> ProbeAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ToolProbeResult> ProbeAsync(RegisteredToolKind kind, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
