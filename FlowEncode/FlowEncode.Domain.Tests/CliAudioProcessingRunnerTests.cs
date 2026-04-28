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
        StringAssert.Contains(command, "-f wav pipe:1 |");
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

    [TestMethod]
    public async Task CreateOpusPipelineShellStartInfo_WhenCommandContainsChinesePaths_PreservesExecutableArguments()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("cmd.exe pipeline validation is only applicable on Windows.");
        }

        var root = Path.Combine(Path.GetTempPath(), $"FlowEncode 中文路径 {Guid.NewGuid():N}");
        var inputPath = Path.Combine(root, "输入 文件.txt");
        var outputPath = Path.Combine(root, "输出 文件.txt");

        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(inputPath, "ok");

            var comSpec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            var command = $"{Quote(comSpec)} /d /c type {Quote(inputPath)} | {Quote(comSpec)} /d /c findstr ok > {Quote(outputPath)}";
            var startInfo = CliAudioProcessingRunner.CreateOpusPipelineShellStartInfo(command);

            StringAssert.Contains(startInfo.Arguments, "中文路径");
            StringAssert.Contains(startInfo.Arguments, "输入 文件.txt");
            Assert.IsFalse(startInfo.Arguments.Contains(".cmd", StringComparison.OrdinalIgnoreCase));

            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start cmd.exe.");
            await process.WaitForExitAsync();

            Assert.AreEqual(0, process.ExitCode);
            Assert.AreEqual("ok", (await File.ReadAllTextAsync(outputPath)).Trim());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
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

    private static string Quote(string value) => $"\"{value}\"";

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
