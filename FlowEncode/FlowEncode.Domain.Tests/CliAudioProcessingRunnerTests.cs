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
        var runner = CreateRunner();
        var request = CreateRequest(useOpusMappingFamily1: false);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "ffmpeg.exe");
        StringAssert.Contains(command, "opusenc.exe");
        StringAssert.Contains(command, "-f wav pipe:1 |");
        Assert.IsFalse(command.Contains("--quiet", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("-loglevel error", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("-mapping_family 1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildDisplayCommand_WhenOpusMappingFamilyIsEnabled_UsesFfmpegLibopus()
    {
        var runner = CreateRunner();
        var request = CreateRequest(useOpusMappingFamily1: true, channelLayout: "5.1", channelCount: 6);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "ffmpeg.exe");
        StringAssert.Contains(command, "-c:a libopus");
        StringAssert.Contains(command, "-mapping_family 1");
        Assert.IsFalse(command.Contains("-loglevel error", StringComparison.Ordinal));
        Assert.IsFalse(command.Contains("opusenc.exe", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildDisplayCommand_WhenOpusMappingFamilyIsEnabledForSideSurround_NormalizesLayout()
    {
        var runner = CreateRunner();
        var request = CreateRequest(useOpusMappingFamily1: true, channelLayout: "5.1(side)", channelCount: 6);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "-filter:a");
        StringAssert.Contains(command, "channelmap=map=FL-FL|FR-FR|FC-FC|LFE-LFE|SL-BL|SR-BR:channel_layout=5.1");
        StringAssert.Contains(command, "-mapping_family 1");
        Assert.IsFalse(command.Contains("opusenc.exe", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildDisplayCommand_WhenOpusMappingFamilyIsEnabledForUnsupportedLayout_FallsBackToOpusencPipe()
    {
        var runner = CreateRunner();
        var request = CreateRequest(useOpusMappingFamily1: true, channelLayout: "4.0", channelCount: 4);

        var command = runner.BuildDisplayCommand(request);

        StringAssert.Contains(command, "opusenc.exe");
        StringAssert.Contains(command, "-f wav pipe:1 |");
        Assert.IsFalse(command.Contains("-mapping_family 1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateOpusPipelineStartInfos_WhenPathsContainChinese_UsesDirectProcessArguments()
    {
        var request = CreateRequest(
            useOpusMappingFamily1: false,
            sourcePath: @"Z:\cmct发布\audio\2-中影公映国配-zh.dts",
            outputPath: @"Z:\cmct发布\audio\2-中影公映国配-zh.opus");

        var startInfos = CliAudioProcessingRunner.CreateOpusPipelineStartInfos(
            request,
            @"C:\工具\ffmpeg.exe",
            @"C:\工具\opusenc.exe",
            @"C:\临时目录\progress.log");

        Assert.AreEqual(@"C:\工具\ffmpeg.exe", startInfos.FfmpegStartInfo.FileName);
        Assert.AreEqual(@"C:\工具\opusenc.exe", startInfos.OpusEncoderStartInfo.FileName);
        CollectionAssert.Contains(startInfos.FfmpegStartInfo.ArgumentList, request.SourcePath);
        CollectionAssert.Contains(startInfos.FfmpegStartInfo.ArgumentList, @"C:\临时目录\progress.log");
        CollectionAssert.Contains(startInfos.OpusEncoderStartInfo.ArgumentList, "--ignorelength");
        CollectionAssert.Contains(startInfos.OpusEncoderStartInfo.ArgumentList, request.OutputPath);
        Assert.AreEqual(string.Empty, startInfos.FfmpegStartInfo.Arguments);
        Assert.AreEqual(string.Empty, startInfos.OpusEncoderStartInfo.Arguments);
    }

    [TestMethod]
    public void CreateRunPlan_WhenOpusRequested_StagesOutputUntilSuccess()
    {
        var request = CreateRequest(
            useOpusMappingFamily1: false,
            outputPath: @"D:\audio\输出 文件.opus");

        var runPlan = CliAudioProcessingRunner.CreateRunPlan(request);

        Assert.AreEqual(request.OutputPath, runPlan.DisplayRequest.OutputPath);
        Assert.AreNotEqual(request.OutputPath, runPlan.ExecutionRequest.OutputPath);
        Assert.AreEqual(runPlan.ExecutionRequest.OutputPath, runPlan.StagedOutputPath);
        StringAssert.Contains(runPlan.ExecutionRequest.OutputPath, ".staging.tmp.opus");
    }

    private static CliAudioProcessingRunner CreateRunner() =>
        new(new StubToolProbeService(), new StubSettingsService());

    private static AudioProcessingRequest CreateRequest(
        bool useOpusMappingFamily1,
        string sourcePath = @"D:\audio\input.thd",
        string outputPath = @"D:\audio\output.opus",
        int? channelCount = 6,
        string? channelLayout = "5.1")
    {
        return new AudioProcessingRequest(
            Guid.NewGuid(),
            sourcePath,
            outputPath,
            AudioProcessingMode.Opus,
            null,
            [],
            120d,
            channelCount,
            channelLayout,
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

    private sealed class StubSettingsService : IAppSettingsService
    {
        public AppSettings Load() => AppSettings.Default with { Language = AppLanguage.English };

        public void Save(AppSettings settings)
        {
        }
    }
}
