using FlowEncode.Application;
using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class LocalEncodingJobRunnerSmokeTests
{
    private static readonly string SmokeRoot = Path.Combine(
        Path.GetTempPath(),
        "FlowEncodeSmoke");

    [TestMethod]
    public async Task RunAsync_X264_Y4mSinglePass_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var outputPath = Path.Combine(SmokeRoot, "x264-smoke.264");
        var runner = CreateRunner(EncoderKind.X264, @"E:\cmct_encode\encoders\x264\x64\x264.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X264,
                "smoke-x264",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                32,
                null,
                "264",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X265_Y4mSinglePass_WithSpacedMetadataPath_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var metadataDirectory = Path.Combine(SmokeRoot, "hdr meta");
        Directory.CreateDirectory(metadataDirectory);
        var metadataPath = Path.Combine(metadataDirectory, "hdr10plus.json");
        await File.WriteAllTextAsync(metadataPath, "{}");
        var outputPath = Path.Combine(SmokeRoot, "x265-smoke.hevc");
        var runner = CreateRunner(EncoderKind.X265, @"E:\cmct_encode\encoders\x265\x64\x265.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X265,
                "smoke-x265",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                32,
                null,
                "hevc",
                $"--dhdr10-info \"{metadataPath}\"",
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X264_Y4mTwoPass_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var outputPath = Path.Combine(SmokeRoot, "x264-2pass-smoke.264");
        var runner = CreateRunner(EncoderKind.X264, @"E:\cmct_encode\encoders\x264\x64\x264.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X264,
                "smoke-x264-2pass",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.TwoPass,
                0,
                600,
                "264",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X265_Y4mTwoPass_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var outputPath = Path.Combine(SmokeRoot, "x265-2pass-smoke.hevc");
        var runner = CreateRunner(EncoderKind.X265, @"E:\cmct_encode\encoders\x265\x64\x265.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X265,
                "smoke-x265-2pass",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.TwoPass,
                0,
                600,
                "hevc",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_SvtAv1_Y4mTwoPass_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var outputPath = Path.Combine(SmokeRoot, "svt-2pass-smoke.ivf");
        var runner = CreateRunner(EncoderKind.SvtAv1, @"E:\cmct_encode\encoders\svt-av1\x64\SvtAv1EncApp.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.SvtAv1,
                "smoke-svt-2pass",
                "smoke",
                "8",
                string.Empty,
                string.Empty,
                RateControlMode.TwoPass,
                0,
                600,
                "ivf",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_SvtAv1_Y4mSinglePass_Completes()
    {
        var sourcePath = EnsureSmokeSource();
        var outputPath = Path.Combine(SmokeRoot, "svt-smoke.ivf");
        var runner = CreateRunner(EncoderKind.SvtAv1, @"E:\cmct_encode\encoders\svt-av1\x64\SvtAv1EncApp.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.SvtAv1,
                "smoke-svt",
                "smoke",
                "8",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                40,
                null,
                "ivf",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Y4mFile,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X264_FfmpegPipeSinglePass_Completes()
    {
        var sourcePath = EnsureSmokeMp4();
        var outputPath = Path.Combine(SmokeRoot, "x264-ffmpeg-pipe-smoke.264");
        var runner = CreateRunner(EncoderKind.X264, @"E:\cmct_encode\encoders\x264\x64\x264.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X264,
                "smoke-x264-ffmpeg-pipe",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                32,
                null,
                "264",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Auto,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X264_VspipeSinglePass_Completes()
    {
        var sourcePath = EnsureSmokeVpy();
        var outputPath = Path.Combine(SmokeRoot, "x264-vspipe-smoke.264");
        var runner = CreateRunner(EncoderKind.X264, @"E:\cmct_encode\encoders\x264\x64\x264.exe");

        var result = await runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X264,
                "smoke-x264-vspipe",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                32,
                null,
                "264",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Auto,
            EncoderArchitecture.X64));

        Assert.AreEqual(EncodingJobState.Completed, result.State);
        Assert.IsTrue(File.Exists(outputPath));
        Assert.IsTrue(new FileInfo(outputPath).Length > 0);
    }

    [TestMethod]
    public async Task RunAsync_X265_VspipeSinglePass_Cancellation_CleansUpProcesses()
    {
        var sourcePath = EnsureLongRunningSmokeVpy();
        var outputPath = Path.Combine(SmokeRoot, "x265-vspipe-cancel-smoke.hevc");
        var runner = CreateRunner(EncoderKind.X265, @"E:\cmct_encode\encoders\x265\x64\x265.exe");
        using var cancellationTokenSource = new CancellationTokenSource();
        using var progressSignal = new ManualResetEventSlim(false);

        var progress = new Progress<EncodingJobProgress>(value =>
        {
            if (value.State == EncodingJobState.Running)
            {
                progressSignal.Set();
            }
        });

        var task = runner.RunAsync(new EncodingJobRequest(
            Guid.NewGuid(),
            new EncodingProfile(
                EncoderKind.X265,
                "smoke-x265-vspipe-cancel",
                "smoke",
                "medium",
                string.Empty,
                string.Empty,
                RateControlMode.Crf,
                32,
                null,
                "hevc",
                string.Empty,
                string.Empty),
            sourcePath,
            outputPath,
            InputPipelineKind.Auto,
            EncoderArchitecture.X64), progress, cancellationTokenSource.Token);

        Assert.IsTrue(progressSignal.Wait(TimeSpan.FromSeconds(5)), "Encoding did not enter running state before cancellation.");
        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(50));

        var result = await task;

        Assert.AreEqual(EncodingJobState.Cancelled, result.State);
        AssertNoRunningProcess("x265");
        AssertNoRunningProcess("vspipe");
    }

    private static LocalEncodingJobRunner CreateRunner(EncoderKind kind, string executablePath)
    {
        Directory.CreateDirectory(SmokeRoot);
        var paths = new LocalAppPaths();
        var discovery = new FakeEncoderDiscoveryService(kind, executablePath);
        var settings = new FakeAppSettingsService(AppSettings.Default with
        {
            PreferSystemEncoders = false,
            Language = AppLanguage.English
        });
        return new LocalEncodingJobRunner(paths, discovery, settings);
    }

    private static string EnsureSmokeSource()
    {
        Directory.CreateDirectory(SmokeRoot);
        var sourcePath = Path.Combine(SmokeRoot, "tiny64.y4m");
        if (File.Exists(sourcePath))
        {
            return sourcePath;
        }

        const int width = 64;
        const int height = 64;
        const int frames = 2;
        using var stream = File.Open(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes($"YUV4MPEG2 W{width} H{height} F24:1 Ip A0:0 C420jpeg\n"));
        for (var index = 0; index < frames; index++)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("FRAME\n"));
            writer.Write(Enumerable.Repeat((byte)(16 + index * 40), width * height).ToArray());
            writer.Write(Enumerable.Repeat((byte)128, (width / 2) * (height / 2)).ToArray());
            writer.Write(Enumerable.Repeat((byte)128, (width / 2) * (height / 2)).ToArray());
        }

        return sourcePath;
    }

    private static string EnsureSmokeMp4()
    {
        var y4mPath = EnsureSmokeSource();
        var mp4Path = Path.Combine(SmokeRoot, "tiny64.mp4");
        if (File.Exists(mp4Path) && new FileInfo(mp4Path).Length > 0)
        {
            return mp4Path;
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = @"E:\cmct_encode\tools\ffmpeg.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ArgumentList =
            {
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                y4mPath,
                "-frames:v",
                "2",
                "-c:v",
                "libx264",
                "-pix_fmt",
                "yuv420p",
                mp4Path
            }
        }) ?? throw new InvalidOperationException("Failed to start ffmpeg for smoke asset generation.");

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Failed to generate smoke mp4. ExitCode={process.ExitCode}. {stderr}");
        }

        return mp4Path;
    }

    private static string EnsureSmokeVpy()
    {
        Directory.CreateDirectory(SmokeRoot);
        var scriptPath = Path.Combine(SmokeRoot, "tiny.vpy");
        if (File.Exists(scriptPath))
        {
            return scriptPath;
        }

        File.WriteAllText(
            scriptPath,
            "import vapoursynth as vs\r\n"
            + "core = vs.core\r\n"
            + "clip = core.std.BlankClip(width=64, height=64, format=vs.YUV420P8, length=2, fpsnum=24, fpsden=1, color=(16, 128, 128))\r\n"
            + "clip.set_output()\r\n");
        return scriptPath;
    }

    private static string EnsureLongRunningSmokeVpy()
    {
        Directory.CreateDirectory(SmokeRoot);
        var scriptPath = Path.Combine(SmokeRoot, "tiny-long.vpy");
        if (File.Exists(scriptPath))
        {
            return scriptPath;
        }

        File.WriteAllText(
            scriptPath,
            "import vapoursynth as vs\r\n"
            + "core = vs.core\r\n"
            + "clip = core.std.BlankClip(width=1280, height=720, format=vs.YUV420P8, length=600, fpsnum=24, fpsden=1, color=(16, 128, 128))\r\n"
            + "clip = core.std.Loop(clip, times=2)\r\n"
            + "clip.set_output()\r\n");
        return scriptPath;
    }

    private static void AssertNoRunningProcess(string processName)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (true)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }

                return;
            }

            foreach (var process in processes)
            {
                process.Dispose();
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.AreEqual(0, processes.Length, $"Expected no running {processName} process after cancellation, but found {processes.Length}.");
            }

            Thread.Sleep(100);
        }
    }

    private sealed class FakeEncoderDiscoveryService : IEncoderDiscoveryService
    {
        private readonly EncoderKind _kind;
        private readonly string _executablePath;

        public FakeEncoderDiscoveryService(EncoderKind kind, string executablePath)
        {
            _kind = kind;
            _executablePath = executablePath;
        }

        public IReadOnlyList<DiscoveredEncoderBinary> DiscoverSystemBinaries()
        {
            return
            [
                new DiscoveredEncoderBinary(
                    _kind,
                    EncoderArchitecture.X64,
                    _executablePath,
                    EncoderBinarySource.Path,
                    "smoke",
                    "test")
            ];
        }

        public DiscoveredEncoderBinary? ResolveEncoder(EncoderKind kind, EncoderArchitecture preferredArchitecture, bool preferSystemEncoders)
        {
            return kind == _kind
                ? new DiscoveredEncoderBinary(
                    _kind,
                    EncoderArchitecture.X64,
                    _executablePath,
                    EncoderBinarySource.Path,
                    "smoke",
                    "test")
                : null;
        }
    }

    private sealed class FakeAppSettingsService : IAppSettingsService
    {
        private AppSettings _settings;

        public FakeAppSettingsService(AppSettings settings)
        {
            _settings = settings;
        }

        public AppSettings Load() => _settings;

        public void Save(AppSettings settings)
        {
            _settings = settings;
        }
    }
}
