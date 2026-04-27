using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class LocalEncodingJobRunner : IEncodingJobRunner
{
    private const string TempWorkspaceFolderName = ".flowencode-temp";
    private static readonly char[] ForbiddenCmdCharacters = ['&', '|', '<', '>', '^', '%'];
    private static readonly TimeSpan TransientProgressReportInterval = TimeSpan.FromMilliseconds(125);
    private static readonly Regex X26xProgressRegex = new(@"(?<progress>\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);
    private static readonly Regex X265PipeMetricsRegex = new(@"^\[\s*(?<progress>\d{1,3}(?:\.\d+)?)\s*%\]\s+(?<current>\d+)\s*\/\s*(?<total>\d+)\s+Frames\s+@\s+(?<fps>\d+(?:\.\d+)?)\s+FPS\s+\|\s+(?<bitrate>\d+(?:\.\d+)?)\s+kb\/s\s+\|\s+(?<eta>\d+:\d{2}:\d{2})(?:\s+\[(?<remainingeta>-?\d+:\d{2}:\d{2})\])?\s+\|\s+(?<currentsize>\d+(?:\.\d+)?)\s*(?<currentunit>[KMGTP]?B)(?:\s+\[(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?B)\])?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xMetricsRegex = new(@"\[?\s*(?<progress>\d{1,3}(?:\.\d+)?)\s*%\]?\s+(?:(?<current>\d+)\s*\/\s*(?<total>\d+)\s+frames|(?<framesonly>\d+)\s+frames:)\s*,?\s*(?<fps>\d+(?:\.\d+)?)\s+fps,\s*(?<bitrate>\d+(?:\.\d+)?)\s+kb/s(?:,\s*eta\s+(?<eta>\d+:\d{2}:\d{2}))?(?:,\s*est\.\s*file\s*size\s+(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?B))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xFrameMetricsRegex = new(@"(?<current>\d+)\s+frames:\s*(?<fps>\d+(?:\.\d+)?)\s+fps,\s*(?<bitrate>\d+(?:\.\d+)?)\s+kb/s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xEncodedSummaryRegex = new(@"^encoded\s+(?<current>\d+)\s+frames,\s+(?<fps>\d+(?:\.\d+)?)\s+fps,\s+(?<bitrate>\d+(?:\.\d+)?)\s+kb/s$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xFrameRatioRegex = new(@"(?<current>\d+)\s*\/\s*(?<total>\d+)\s+frames?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xFrameEqualsRegex = new(@"\bframe=\s*(?<current>\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xLooseFpsRegex = new(@"(?<fps>\d+(?:\.\d+)?)\s*fps\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xLooseBitrateRegex = new(@"(?<bitrate>\d+(?:\.\d+)?)\s*kb(?:\/s|ps)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xLooseEtaRegex = new(@"(?:eta|time)\s*:?\s*(?<eta>-?\d+:\d{2}:\d{2})(?:\s*\[(?<remainingeta>-?\d+:\d{2}:\d{2})\])?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xLooseSizeRegex = new(@"(?:est\.\s*file\s*size|size)\s*:?\s*(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?B)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex X26xBracketedSizeRegex = new(@"\[(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?B)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvtFrameRegex = new(@"Encoding\s+frame\s+(?<frame>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvtOutputRegex = new(@"Output\s+(?<frame>\d+)\s+frames", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvtStatusMetricsRegex = new(
        @"^Encoding:\s*(?<current>\d+)\s*\/\s*(?<total>\d+)\s+Frames?\s+@\s*(?<fps>\d+(?:\.\d+)?)\s+fps\s*\|\s*(?<bitrate>\d+(?:\.\d+)?)\s+kbps\s*\|\s*Time:\s*(?<elapsed>-?\d+:\d{2}:\d{2})(?:\s*\[(?<eta>-?\d+:\d{2}:\d{2})\])?\s*\|\s*Size:\s*(?<currentsize>\d+(?:\.\d+)?)\s*(?<currentunit>[KMGTP]?B)(?:\s*\[(?<size>\d+(?:\.\d+)?)\s*(?<unit>[KMGTP]?B)\])?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvtLooseMetricsRegex = new(
        @"^Encoding:\s*(?<current>\d+)\s*\/\s*(?<total>\d+)\s+Frames?\b.*?(?<fps>\d+(?:\.\d+)?)\s+fps\b.*?(?<bitrate>\d+(?:\.\d+)?)\s+kbps\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvtMetricsRegex = new(@"Encoding\s+frame\s+(?<current>\d+)\s+(?<bitrate>\d+(?:\.\d+)?)\s+kbps\s+(?<fps>\d+(?:\.\d+)?)\s+fps", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ExternalToolLocator _toolLocator;
    private readonly SourceVideoInfoProbe _sourceInfoProbe;
    private readonly IEncoderDiscoveryService _discoveryService;
    private readonly IAppSettingsService _settingsService;
    private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

    public LocalEncodingJobRunner(
        LocalAppPaths paths,
        IEncoderDiscoveryService discoveryService,
        IAppSettingsService settingsService)
    {
        _toolLocator = new ExternalToolLocator(paths);
        _sourceInfoProbe = new SourceVideoInfoProbe(_toolLocator);
        _discoveryService = discoveryService;
        _settingsService = settingsService;
    }

    public string BuildDisplayCommand(EncodingJobRequest request)
    {
        var encoderPath = ResolveEncoderPath(request);
        return BuildPlan(request, encoderPath, includeSourceMetadata: false).DisplayCommand;
    }

    public void AbortJob(Guid jobId)
    {
        if (_activeProcesses.TryGetValue(jobId, out var process))
        {
            TryTerminateProcess(jobId, process);
        }
    }

    public async Task<EncodingJobResult> RunAsync(
        EncodingJobRequest request,
        IProgress<EncodingJobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var encoderPath = ResolveEncoderPath(request);
        var plan = BuildPlan(request, encoderPath, includeSourceMetadata: true);
        var visibleLogBuilder = new StringBuilder();
        var currentState = EncodingJobState.Running;
        var progressDispatchState = new ProgressDispatchState(DateTimeOffset.UtcNow, 0.0, 0);
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        var rawLogPath = CreateTemporaryRawLogPath(request);
        var lineGate = new object();
        var rawLogWriter = CreateRawLogWriter(rawLogPath);
        var rawLogWriterClosed = false;
        Task pumpOutput = Task.CompletedTask;
        Task pumpError = Task.CompletedTask;
        Process? activeProcess = null;

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        async Task CloseRawLogWriterAsync()
        {
            if (rawLogWriterClosed)
            {
                return;
            }

            rawLogWriterClosed = true;

            try
            {
                await rawLogWriter.FlushAsync();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            await rawLogWriter.DisposeAsync();
        }

        progress?.Report(new EncodingJobProgress(
            request.JobId,
            EncodingJobState.Running,
            0.0,
            "编码已启动",
            plan.DisplayCommand,
            BuildInitialSnapshot(plan)));

        try
        {
            foreach (var step in plan.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AppendStageHeader(step, rawLogWriter, visibleLogBuilder);
                progress?.Report(new EncodingJobProgress(
                    request.JobId,
                    EncodingJobState.Running,
                    BuildStageStartingProgress(step),
                    BuildStageStartingSummary(step),
                    BuildStageStartingDetail(step),
                    BuildStageStartingSnapshot(plan, step)));

                using var process = CreateProcess(step, encoderPath);
                activeProcess = process;

                process.Start();
                _activeProcesses[request.JobId] = process;

                void HandleLine(string line)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        return;
                    }

                    EncodingJobProgress? pendingProgress = null;

                    lock (lineGate)
                    {
                        rawLogWriter.WriteLine(line);

                        if (!EncodingLogLineClassifier.IsTransientProgressLine(plan.Kind, line))
                        {
                            visibleLogBuilder.AppendLine(line);
                        }

                        var progressSnapshot = ParseProgressSnapshot(plan.Kind, plan.TotalFrames, plan.SourceFramesPerSecond, line);
                        var stageAwareProgress = ApplyStageProgress(progressSnapshot, step);
                        if (!ShouldReportProgress(plan.Kind, line, stageAwareProgress, ref progressDispatchState))
                        {
                            return;
                        }

                        pendingProgress = new EncodingJobProgress(
                            request.JobId,
                            currentState,
                            stageAwareProgress?.ProgressFraction,
                            BuildRunningSummary(step, stageAwareProgress?.ProgressFraction),
                            line,
                            stageAwareProgress?.Snapshot);
                    }

                    if (pendingProgress is not null)
                    {
                        progress?.Report(pendingProgress);
                    }
                }

                pumpOutput = PumpAsync(process.StandardOutput, HandleLine, cancellationToken);
                pumpError = PumpAsync(process.StandardError, HandleLine, cancellationToken);

                await process.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(pumpOutput, pumpError);
                _activeProcesses.TryRemove(request.JobId, out _);
                activeProcess = null;

                if (process.ExitCode != 0)
                {
                    currentState = EncodingJobState.Failed;
                    var failedSummary = step.StageCount > 1
                        ? $"第 {step.StageIndex}/{step.StageCount} 遍失败，退出代码 {process.ExitCode}"
                        : $"编码失败，退出代码 {process.ExitCode}";
                    var failedVisibleLog = visibleLogBuilder.ToString();
                    await CloseRawLogWriterAsync();
                    var failedSidecarLogPath = await WriteSidecarLogAsync(request, plan.DisplayCommand, currentState, process.ExitCode, rawLogPath);

                    progress?.Report(new EncodingJobProgress(
                        request.JobId,
                        currentState,
                        BuildStageFailureProgress(step),
                        failedSummary,
                        LastMeaningfulLine(failedVisibleLog)));

                    return new EncodingJobResult(
                        request.JobId,
                        currentState,
                        process.ExitCode,
                        failedSummary,
                        failedVisibleLog,
                        failedSidecarLogPath);
                }
            }

            currentState = EncodingJobState.Completed;
            var summary = "编码完成";
            var visibleLog = visibleLogBuilder.ToString();
            await CloseRawLogWriterAsync();
            var sidecarLogPath = await WriteSidecarLogAsync(request, plan.DisplayCommand, currentState, 0, rawLogPath);

            progress?.Report(new EncodingJobProgress(
                request.JobId,
                currentState,
                1.0,
                summary,
                LastMeaningfulLine(visibleLog)));

            return new EncodingJobResult(
                request.JobId,
                currentState,
                0,
                summary,
                visibleLog,
                sidecarLogPath);
        }
        catch (OperationCanceledException)
        {
            currentState = EncodingJobState.Cancelled;

            try
            {
                if (activeProcess is not null && !activeProcess.HasExited)
                {
                    TryTerminateProcess(request.JobId, activeProcess);
                }
            }
            catch
            {
            }

            progress?.Report(new EncodingJobProgress(
                request.JobId,
                currentState,
                null,
                "编码已取消",
                "作业已被用户取消。"));

            var cancelledLog = visibleLogBuilder.ToString();

            try
            {
                await Task.WhenAll(pumpOutput, pumpError);
            }
            catch (OperationCanceledException)
            {
            }

            await CloseRawLogWriterAsync();
            var cancelledLogPath = await WriteSidecarLogAsync(request, plan.DisplayCommand, currentState, -1, rawLogPath);
            return new EncodingJobResult(request.JobId, currentState, -1, "编码已取消", cancelledLog, cancelledLogPath);
        }
        finally
        {
            _activeProcesses.TryRemove(request.JobId, out _);
            CleanupPlanArtifacts(plan);

            if (!rawLogWriterClosed)
            {
                await rawLogWriter.DisposeAsync();
            }

            CleanupTemporaryRawLog(rawLogPath);
            CleanupJobTempDirectory(request);
        }
    }

    private string ResolveEncoderPath(EncodingJobRequest request)
    {
        var settings = _settingsService.Load();
        var resolved = _discoveryService.ResolveEncoder(
            request.Profile.Kind,
            request.PreferredArchitecture,
            settings.PreferSystemEncoders);

        if (!string.IsNullOrWhiteSpace(resolved?.ExecutablePath) && File.Exists(resolved.ExecutablePath))
        {
            return resolved.ExecutablePath;
        }

        throw new FileNotFoundException($"未找到 {request.Profile.Kind.ToDisplayName()} 可执行文件。请先在工具链页面导入或自动更新编码器。");
    }

    private EncodingExecutionPlan BuildPlan(
        EncodingJobRequest request,
        string encoderPath,
        bool includeSourceMetadata)
    {
        var profile = request.Profile;
        var pipelineKind = ResolvePipelineKind(request);
        ValidateShellPipelineArguments(request, pipelineKind);
        var sourceInfo = includeSourceMetadata || profile.Kind == EncoderKind.SvtAv1
            ? ResolveSourceInfo(
                request,
                pipelineKind,
                profile.Kind == EncoderKind.SvtAv1 && pipelineKind != InputPipelineKind.RawYuvFile)
            : null;
        var preset = EncoderArgumentValueNormalizer.NormalizePresetForCli(profile.Kind, profile.Preset);
        var tune = EncoderArgumentValueNormalizer.NormalizeTuneForCli(profile.Kind, profile.Tune);
        var profileValue = EncoderArgumentValueNormalizer.NormalizeProfileForCli(profile.Kind, profile.Profile);
        var sourceCommand = pipelineKind switch
        {
            InputPipelineKind.VapourSynth => $"{Quote(_toolLocator.ResolveVspipe())} {Quote(request.SourcePath)} - --container y4m",
            InputPipelineKind.AviSynth => $"{Quote(_toolLocator.ResolveAvs2PipeMod())} -y4mp {Quote(request.SourcePath)}",
            InputPipelineKind.FfmpegPipe => $"{Quote(_toolLocator.ResolveFfmpeg())} -hide_banner -loglevel error -i {Quote(request.SourcePath)} -map 0:v:0 -an -sn -dn -f yuv4mpegpipe -",
            InputPipelineKind.RawYuvFile => string.Empty,
            InputPipelineKind.Y4mFile => string.Empty,
            _ => throw new InvalidOperationException("当前输入模式不支持管道执行。")
        };

        var statsPath = profile.RateControl == RateControlMode.TwoPass
            ? BuildMultipassStatsPath(request, profile.Kind)
            : null;
        var includeX265UhdParameters = profile.Kind == EncoderKind.X265
            && !string.IsNullOrWhiteSpace(profile.UhdParameters);

        var steps = BuildExecutionSteps(
            request,
            encoderPath,
            sourceCommand,
            pipelineKind,
            sourceInfo,
            includeX265UhdParameters,
            preset,
            tune,
            profileValue,
            statsPath);

        var displayCommand = JoinStepDisplayCommands(steps);

        return new EncodingExecutionPlan(
            steps,
            displayCommand,
            profile.Kind,
            sourceInfo?.TotalFrames,
            sourceInfo?.FramesPerSecond,
            statsPath is null ? [] : [statsPath]);
    }

    private static IReadOnlyList<EncodingExecutionStep> BuildExecutionSteps(
        EncodingJobRequest request,
        string encoderPath,
        string sourceCommand,
        InputPipelineKind pipelineKind,
        SourceVideoInfo? sourceInfo,
        bool includeX265UhdParameters,
        string preset,
        string tune,
        string profileValue,
        string? statsPath)
    {
        return request.Profile.Kind switch
        {
            EncoderKind.X264 or EncoderKind.X265 when request.Profile.RateControl == RateControlMode.TwoPass
                => BuildX26xTwoPassSteps(request, encoderPath, sourceCommand, pipelineKind, includeX265UhdParameters, preset, tune, profileValue, statsPath!),
            EncoderKind.SvtAv1 when request.Profile.RateControl == RateControlMode.TwoPass
                => BuildSvtTwoPassSteps(request, encoderPath, sourceCommand, pipelineKind, sourceInfo, preset, tune, profileValue, statsPath!),
            _ => BuildSinglePassSteps(request, encoderPath, sourceCommand, pipelineKind, sourceInfo, includeX265UhdParameters, preset, tune, profileValue, statsPath)
        };
    }

    private static IReadOnlyList<EncodingExecutionStep> BuildSinglePassSteps(
        EncodingJobRequest request,
        string encoderPath,
        string sourceCommand,
        InputPipelineKind pipelineKind,
        SourceVideoInfo? sourceInfo,
        bool includeX265UhdParameters,
        string preset,
        string tune,
        string profileValue,
        string? statsPath)
    {
        var encoderCommand = BuildEncoderCommand(
            request,
            encoderPath,
            pipelineKind,
            sourceInfo,
            includeX265UhdParameters,
            preset,
            tune,
            profileValue,
            request.OutputPath,
            BuildRateControlArguments(request.Profile.Kind, request.Profile, statsPath));

        return [CreateExecutionStep(sourceCommand, pipelineKind, encoderPath, encoderCommand, 1, 1)];
    }

    private static IReadOnlyList<EncodingExecutionStep> BuildX26xTwoPassSteps(
        EncodingJobRequest request,
        string encoderPath,
        string sourceCommand,
        InputPipelineKind pipelineKind,
        bool includeX265UhdParameters,
        string preset,
        string tune,
        string profileValue,
        string statsPath)
    {
        var pass1Command = BuildEncoderCommand(
            request,
            encoderPath,
            pipelineKind,
            sourceInfo: null,
            includeX265UhdParameters,
            preset,
            tune,
            profileValue,
            "NUL",
            BuildRateControlArguments(request.Profile.Kind, request.Profile, statsPath, passIndex: 1, passCount: 2));

        var pass2Command = BuildEncoderCommand(
            request,
            encoderPath,
            pipelineKind,
            sourceInfo: null,
            includeX265UhdParameters,
            preset,
            tune,
            profileValue,
            request.OutputPath,
            BuildRateControlArguments(request.Profile.Kind, request.Profile, statsPath, passIndex: 2, passCount: 2));

        return
        [
            CreateExecutionStep(sourceCommand, pipelineKind, encoderPath, pass1Command, 1, 2),
            CreateExecutionStep(sourceCommand, pipelineKind, encoderPath, pass2Command, 2, 2)
        ];
    }

    private static IReadOnlyList<EncodingExecutionStep> BuildSvtTwoPassSteps(
        EncodingJobRequest request,
        string encoderPath,
        string sourceCommand,
        InputPipelineKind pipelineKind,
        SourceVideoInfo? sourceInfo,
        string preset,
        string tune,
        string profileValue,
        string statsPath)
    {
        var resolvedSourceInfo = sourceInfo
            ?? throw new InvalidOperationException("SVT-AV1 两遍编码需要可探测的源信息。");

        var pass1Command = BuildEncoderCommand(
            request,
            encoderPath,
            pipelineKind,
            resolvedSourceInfo,
            includeX265UhdParameters: false,
            preset,
            tune,
            profileValue,
            "NUL",
            BuildRateControlArguments(request.Profile.Kind, request.Profile, statsPath, passIndex: 1, passCount: 2));

        var pass2Command = BuildEncoderCommand(
            request,
            encoderPath,
            pipelineKind,
            resolvedSourceInfo,
            includeX265UhdParameters: false,
            preset,
            tune,
            profileValue,
            request.OutputPath,
            BuildRateControlArguments(request.Profile.Kind, request.Profile, statsPath, passIndex: 2, passCount: 2));

        return
        [
            CreateExecutionStep(sourceCommand, pipelineKind, encoderPath, pass1Command, 1, 2),
            CreateExecutionStep(sourceCommand, pipelineKind, encoderPath, pass2Command, 2, 2)
        ];
    }

    private static EncodingExecutionStep CreateExecutionStep(
        string sourceCommand,
        InputPipelineKind pipelineKind,
        string encoderPath,
        string encoderCommand,
        int stageIndex,
        int stageCount)
    {
        if (pipelineKind is InputPipelineKind.Y4mFile or InputPipelineKind.RawYuvFile)
        {
            return new EncodingExecutionStep(
                encoderPath,
                encoderCommand[(Quote(encoderPath).Length)..].TrimStart(),
                encoderCommand,
                stageIndex,
                stageCount);
        }

        var pipelineCommand = $"{sourceCommand} | {encoderCommand}";
        return new EncodingExecutionStep(
            "cmd.exe",
            $"/d /s /c \"{pipelineCommand}\"",
            pipelineCommand,
            stageIndex,
            stageCount);
    }

    private static string BuildEncoderCommand(
        EncodingJobRequest request,
        string encoderPath,
        InputPipelineKind pipelineKind,
        SourceVideoInfo? sourceInfo,
        bool includeX265UhdParameters,
        string preset,
        string tune,
        string profileValue,
        string outputPath,
        string rateControl)
    {
        var profile = request.Profile;
        var outputArg = profile.Kind switch
        {
            EncoderKind.X264 => $"-o {Quote(outputPath)}",
            EncoderKind.X265 => $"-o {Quote(outputPath)}",
            EncoderKind.SvtAv1 => $"-b {Quote(outputPath)}",
            _ => throw new ArgumentOutOfRangeException()
        };

        var directInputArgs = profile.Kind switch
        {
            EncoderKind.X264 => BuildX264DirectInputArguments(request.SourcePath, pipelineKind),
            EncoderKind.X265 => BuildX265DirectInputArguments(request.SourcePath, pipelineKind),
            EncoderKind.SvtAv1 => BuildSvtDirectInputArguments(request.SourcePath, pipelineKind),
            _ => throw new ArgumentOutOfRangeException()
        };

        return profile.Kind switch
        {
            EncoderKind.X264 => JoinArguments(
                Quote(encoderPath),
                $"--preset {preset}",
                rateControl,
                Optional("--tune", tune),
                Optional("--profile", profileValue),
                directInputArgs,
                OptionalSegment(profile.AdditionalArguments),
                outputArg),
            EncoderKind.X265 => JoinArguments(
                Quote(encoderPath),
                $"--preset {preset}",
                rateControl,
                Optional("--tune", tune),
                Optional("--profile", profileValue),
                directInputArgs,
                OptionalSegment(profile.AdditionalArguments),
                OptionalSegment(includeX265UhdParameters ? profile.UhdParameters : string.Empty),
                outputArg),
            EncoderKind.SvtAv1 => JoinArguments(
                Quote(encoderPath),
                $"--preset {preset}",
                rateControl,
                Optional("--tune", tune),
                Optional("--profile", profileValue),
                "--progress 2",
                sourceInfo is null ? string.Empty : BuildSvtSourceArguments(sourceInfo),
                directInputArgs,
                OptionalSegment(profile.AdditionalArguments),
                outputArg),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string JoinStepDisplayCommands(IReadOnlyList<EncodingExecutionStep> steps)
    {
        if (steps.Count == 1)
        {
            return steps[0].DisplayCommand;
        }

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            steps.Select(step => $"[Pass {step.StageIndex}/{step.StageCount}]{Environment.NewLine}{step.DisplayCommand}"));
    }

    private static EncodingProgressSnapshot? BuildInitialSnapshot(EncodingExecutionPlan plan)
    {
        if (plan.TotalFrames is null)
        {
            return null;
        }

        return new EncodingProgressSnapshot(0, plan.TotalFrames, null, null, null, null);
    }

    private static async Task<string> WriteSidecarLogAsync(
        EncodingJobRequest request,
        string displayCommand,
        EncodingJobState state,
        int exitCode,
        string rawLogPath)
    {
        try
        {
            var logPath = GetAvailableLogPath(request);
            await using var stream = File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync($"JobId: {request.JobId}");
            await writer.WriteLineAsync($"Encoder: {request.Profile.Kind.ToDisplayName()}");
            await writer.WriteLineAsync($"State: {state}");
            await writer.WriteLineAsync($"ExitCode: {exitCode}");
            await writer.WriteLineAsync($"Source: {request.SourcePath}");
            await writer.WriteLineAsync($"Output: {request.OutputPath}");
            await writer.WriteLineAsync($"Timestamp: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("--- COMMAND ---");
            await writer.WriteLineAsync(displayCommand);
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("--- LOG ---");

            if (File.Exists(rawLogPath))
            {
                await writer.FlushAsync();
                using var reader = File.OpenText(rawLogPath);
                await reader.BaseStream.CopyToAsync(stream);
            }

            return logPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        var current = new StringBuilder();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var ch = buffer[index];
                if (ch is '\r' or '\n')
                {
                    if (current.Length > 0)
                    {
                        onLine(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            onLine(current.ToString());
        }
    }

    private SourceVideoInfo? ResolveSourceInfo(EncodingJobRequest request, InputPipelineKind pipelineKind, bool required)
    {
        try
        {
            var sourceInfo = _sourceInfoProbe.Probe(request.SourcePath, pipelineKind);
            if (sourceInfo is not null)
            {
                return sourceInfo;
            }
        }
        catch when (!required)
        {
        }

        if (required)
        {
            throw new InvalidOperationException("SVT-AV1 需要可探测的源信息。请确保当前输入可被 ffprobe / vspipe 正常识别。");
        }

        return null;
    }

    private static string BuildRateControlArguments(
        EncoderKind kind,
        EncodingProfile profile,
        string? statsPath = null,
        int? passIndex = null,
        int? passCount = null)
    {
        return profile.RateControl switch
        {
            RateControlMode.Crf => kind == EncoderKind.SvtAv1
                ? $"--rc 0 --crf {FormatNumber(profile.Quality)}"
                : $"--crf {FormatNumber(profile.Quality)}",
            RateControlMode.Cq or RateControlMode.Qp => kind == EncoderKind.SvtAv1
                ? $"--rc 0 --qp {FormatNumber(profile.Quality)}"
                : $"--qp {FormatNumber(profile.Quality)}",
            RateControlMode.Abr or RateControlMode.Vbr => kind == EncoderKind.SvtAv1
                ? $"--rc 1 --tbr {profile.Bitrate ?? 3500}"
                : $"--bitrate {profile.Bitrate ?? 3500}",
            RateControlMode.TwoPass => BuildTwoPassRateControlArguments(kind, profile, statsPath, passIndex, passCount),
            _ => string.Empty
        };
    }

    private static string BuildTwoPassRateControlArguments(
        EncoderKind kind,
        EncodingProfile profile,
        string? statsPath,
        int? passIndex,
        int? passCount)
    {
        return kind switch
        {
            EncoderKind.X264 or EncoderKind.X265 => JoinArguments(
                $"--bitrate {profile.Bitrate ?? 3500}",
                passIndex.HasValue ? $"--pass {passIndex.Value}" : "--pass 1",
                Optional("--stats", QuoteIfPresent(statsPath))),
            EncoderKind.SvtAv1 => JoinArguments(
                $"--rc 1 --tbr {profile.Bitrate ?? 3500}",
                passIndex.HasValue ? $"--pass {passIndex.Value}" : "--pass 1",
                Optional("--stats", QuoteIfPresent(statsPath))),
            _ => string.Empty
        };
    }

    private static string BuildSvtSourceArguments(SourceVideoInfo sourceInfo)
    {
        return JoinArguments(
            $"--width {sourceInfo.Width}",
            $"--height {sourceInfo.Height}",
            sourceInfo.TotalFrames is > 0 ? $"--frames {sourceInfo.TotalFrames.Value}" : string.Empty,
            sourceInfo.BitDepth > 0 ? $"--input-depth {sourceInfo.BitDepth}" : string.Empty);
    }

    private static bool ShouldReportProgress(
        EncoderKind kind,
        string line,
        ParsedProgressSnapshot? progressSnapshot,
        ref ProgressDispatchState state)
    {
        var now = DateTimeOffset.UtcNow;
        var currentProgressFraction = progressSnapshot?.ProgressFraction;
        var currentFrame = progressSnapshot?.Snapshot?.CurrentFrame;
        var isTransient = EncodingLogLineClassifier.IsTransientProgressLine(kind, line);

        if (!isTransient)
        {
            state = new ProgressDispatchState(now, currentProgressFraction, currentFrame);
            return true;
        }

        if (progressSnapshot is null)
        {
            return false;
        }

        var intervalElapsed = now - state.LastReportedAt >= TransientProgressReportInterval;
        var hasMeaningfulProgressDelta = !state.LastProgressFraction.HasValue
            || !currentProgressFraction.HasValue
            || Math.Abs(currentProgressFraction.Value - state.LastProgressFraction.Value) >= 0.0025;
        var frameAdvanced = currentFrame != state.LastCurrentFrame;

        if (!frameAdvanced && !hasMeaningfulProgressDelta)
        {
            return false;
        }

        if (!intervalElapsed && !hasMeaningfulProgressDelta)
        {
            return false;
        }

        state = new ProgressDispatchState(now, currentProgressFraction, currentFrame);
        return true;
    }

    private static ParsedProgressSnapshot? ParseProgressSnapshot(
        EncoderKind kind,
        long? totalFrames,
        double? sourceFramesPerSecond,
        string line)
    {
        if (kind is EncoderKind.X264 or EncoderKind.X265)
        {
            var normalizedX26xLine = NormalizeX26xProgressPrefix(line);

            var x265PipeMetricsMatch = X265PipeMetricsRegex.Match(normalizedX26xLine);
            if (x265PipeMetricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(x265PipeMetricsMatch.Groups["current"].Value);
                var parsedTotalFrames = ParseInvariantLong(x265PipeMetricsMatch.Groups["total"].Value) ?? totalFrames;
                var progress = TryBuildProgressFraction(
                    ParseInvariantDoubleNullable(x265PipeMetricsMatch.Groups["progress"].Value),
                    currentFrame,
                    parsedTotalFrames);
                var fps = ParseInvariantDoubleNullable(x265PipeMetricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(x265PipeMetricsMatch.Groups["bitrate"].Value);
                var eta = ParseEta(x265PipeMetricsMatch.Groups["remainingeta"].Value)
                    ?? ParseEta(x265PipeMetricsMatch.Groups["eta"].Value);
                var estimatedFileSizeBytes =
                    ParseSizeToBytes(x265PipeMetricsMatch.Groups["size"].Value, x265PipeMetricsMatch.Groups["unit"].Value)
                    ?? ParseSizeToBytes(x265PipeMetricsMatch.Groups["currentsize"].Value, x265PipeMetricsMatch.Groups["currentunit"].Value);

                return new ParsedProgressSnapshot(
                    progress,
                    new EncodingProgressSnapshot(currentFrame, parsedTotalFrames, fps, bitrate, eta, estimatedFileSizeBytes));
            }

            var metricsMatch = X26xMetricsRegex.Match(normalizedX26xLine);
            if (metricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(metricsMatch.Groups["current"].Value)
                    ?? ParseInvariantLong(metricsMatch.Groups["framesonly"].Value);
                var parsedTotalFrames = ParseInvariantLong(metricsMatch.Groups["total"].Value) ?? totalFrames;
                var progress = TryBuildProgressFraction(
                    ParseInvariantDoubleNullable(metricsMatch.Groups["progress"].Value),
                    currentFrame,
                    parsedTotalFrames);
                var fps = ParseInvariantDoubleNullable(metricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(metricsMatch.Groups["bitrate"].Value);
                var eta = ParseEta(metricsMatch.Groups["eta"].Value);
                var estimatedFileSizeBytes = ParseSizeToBytes(metricsMatch.Groups["size"].Value, metricsMatch.Groups["unit"].Value);

                return new ParsedProgressSnapshot(
                    progress,
                    new EncodingProgressSnapshot(currentFrame, parsedTotalFrames, fps, bitrate, eta, estimatedFileSizeBytes));
            }

            var frameMetricsMatch = X26xFrameMetricsRegex.Match(normalizedX26xLine);
            if (frameMetricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(frameMetricsMatch.Groups["current"].Value);
                var fps = ParseInvariantDoubleNullable(frameMetricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(frameMetricsMatch.Groups["bitrate"].Value);
                var progressFraction = TryBuildProgressFraction(null, currentFrame, totalFrames);
                var eta = currentFrame.HasValue && totalFrames is > 0 && fps is > 0
                    ? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(0, (totalFrames.Value - currentFrame.Value) / fps.Value))
                    : null;
                var estimatedSizeBytes = totalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0
                    ? (long?)EstimateFileSizeBytes(totalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value)
                    : null;

                return new ParsedProgressSnapshot(
                    progressFraction,
                    new EncodingProgressSnapshot(currentFrame, totalFrames, fps, bitrate, eta, estimatedSizeBytes));
            }

            var encodedSummaryMatch = X26xEncodedSummaryRegex.Match(normalizedX26xLine);
            if (encodedSummaryMatch.Success)
            {
                var currentFrame = ParseInvariantLong(encodedSummaryMatch.Groups["current"].Value);
                var fps = ParseInvariantDoubleNullable(encodedSummaryMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(encodedSummaryMatch.Groups["bitrate"].Value);
                var progressFraction = TryBuildProgressFraction(null, currentFrame, totalFrames);
                var estimatedSizeBytes = totalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0
                    ? (long?)EstimateFileSizeBytes(totalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value)
                    : null;

                return new ParsedProgressSnapshot(
                    progressFraction,
                    new EncodingProgressSnapshot(currentFrame, totalFrames, fps, bitrate, null, estimatedSizeBytes));
            }

            var looseSnapshot = TryParseLooseX26xMetrics(normalizedX26xLine, totalFrames, sourceFramesPerSecond);
            if (looseSnapshot is not null)
            {
                return looseSnapshot;
            }

            var match = X26xProgressRegex.Match(normalizedX26xLine);
            if (match.Success)
            {
                return new ParsedProgressSnapshot(
                    Math.Clamp(ParseInvariantDouble(match.Groups["progress"].Value) / 100.0, 0.0, 1.0),
                    null);
            }
        }

        if (kind == EncoderKind.SvtAv1)
        {
            var statusMetricsMatch = SvtStatusMetricsRegex.Match(line);
            if (statusMetricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(statusMetricsMatch.Groups["current"].Value);
                var parsedTotalFrames = ParseInvariantLong(statusMetricsMatch.Groups["total"].Value) ?? totalFrames;
                var fps = ParseInvariantDoubleNullable(statusMetricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(statusMetricsMatch.Groups["bitrate"].Value);
                var progressFraction = TryBuildProgressFraction(null, currentFrame, parsedTotalFrames);
                var eta = ParseEta(statusMetricsMatch.Groups["eta"].Value)
                    ?? (currentFrame.HasValue && parsedTotalFrames is > 0 && fps is > 0
                        ? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(0, (parsedTotalFrames.Value - currentFrame.Value) / fps.Value))
                        : null);
                var estimatedSizeBytes =
                    ParseSizeToBytes(statusMetricsMatch.Groups["size"].Value, statusMetricsMatch.Groups["unit"].Value)
                    ?? ParseSizeToBytes(statusMetricsMatch.Groups["currentsize"].Value, statusMetricsMatch.Groups["currentunit"].Value)
                    ?? (parsedTotalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0
                        ? (long?)EstimateFileSizeBytes(parsedTotalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value)
                        : null);

                return new ParsedProgressSnapshot(
                    progressFraction,
                    new EncodingProgressSnapshot(currentFrame, parsedTotalFrames, fps, bitrate, eta, estimatedSizeBytes));
            }

            var looseMetricsMatch = SvtLooseMetricsRegex.Match(line);
            if (looseMetricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(looseMetricsMatch.Groups["current"].Value);
                var parsedTotalFrames = ParseInvariantLong(looseMetricsMatch.Groups["total"].Value) ?? totalFrames;
                var fps = ParseInvariantDoubleNullable(looseMetricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(looseMetricsMatch.Groups["bitrate"].Value);
                var progressFraction = TryBuildProgressFraction(null, currentFrame, parsedTotalFrames);
                var eta = currentFrame.HasValue && parsedTotalFrames is > 0 && fps is > 0
                    ? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(0, (parsedTotalFrames.Value - currentFrame.Value) / fps.Value))
                    : null;
                var estimatedSizeBytes = parsedTotalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0
                    ? (long?)EstimateFileSizeBytes(parsedTotalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value)
                    : null;

                return new ParsedProgressSnapshot(
                    progressFraction,
                    new EncodingProgressSnapshot(currentFrame, parsedTotalFrames, fps, bitrate, eta, estimatedSizeBytes));
            }

            var metricsMatch = SvtMetricsRegex.Match(line);
            if (metricsMatch.Success)
            {
                var currentFrame = ParseInvariantLong(metricsMatch.Groups["current"].Value);
                var fps = ParseInvariantDoubleNullable(metricsMatch.Groups["fps"].Value);
                var bitrate = ParseInvariantDoubleNullable(metricsMatch.Groups["bitrate"].Value);
                var progressFraction = TryBuildProgressFraction(null, currentFrame, totalFrames);
                var eta = currentFrame.HasValue && totalFrames is > 0 && fps is > 0
                    ? (TimeSpan?)TimeSpan.FromSeconds(Math.Max(0, (totalFrames.Value - currentFrame.Value) / fps.Value))
                    : null;
                var estimatedSizeBytes = currentFrame.HasValue && totalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0
                    ? (long?)EstimateFileSizeBytes(totalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value)
                    : null;

                return new ParsedProgressSnapshot(
                    progressFraction,
                    new EncodingProgressSnapshot(currentFrame, totalFrames, fps, bitrate, eta, estimatedSizeBytes));
            }

            var match = SvtFrameRegex.Match(line);
            if (!match.Success)
            {
                match = SvtOutputRegex.Match(line);
            }

            if (match.Success
                && totalFrames is > 0
                && long.TryParse(match.Groups["frame"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame))
            {
                return new ParsedProgressSnapshot(
                    Math.Clamp(frame / (double)totalFrames.Value, 0.0, 1.0),
                    new EncodingProgressSnapshot(frame, totalFrames, null, null, null, null));
            }
        }

        return null;
    }

    private static ParsedProgressSnapshot? TryParseLooseX26xMetrics(
        string line,
        long? totalFrames,
        double? sourceFramesPerSecond)
    {
        if (!(line.Contains('%')
              || line.Contains("frame=", StringComparison.OrdinalIgnoreCase)
              || line.Contains("frames", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!line.Contains("fps", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var explicitPercent = ParseInvariantDoubleNullable(X26xProgressRegex.Match(line).Groups["progress"].Value);

        var frameRatioMatch = X26xFrameRatioRegex.Match(line);
        long? currentFrame = null;
        long? parsedTotalFrames = totalFrames;
        if (frameRatioMatch.Success)
        {
            currentFrame = ParseInvariantLong(frameRatioMatch.Groups["current"].Value);
            parsedTotalFrames = ParseInvariantLong(frameRatioMatch.Groups["total"].Value) ?? totalFrames;
        }
        else
        {
            var frameEqualsMatch = X26xFrameEqualsRegex.Match(line);
            if (frameEqualsMatch.Success)
            {
                currentFrame = ParseInvariantLong(frameEqualsMatch.Groups["current"].Value);
            }
        }

        var fps = ParseInvariantDoubleNullable(X26xLooseFpsRegex.Match(line).Groups["fps"].Value);
        var bitrate = ParseInvariantDoubleNullable(X26xLooseBitrateRegex.Match(line).Groups["bitrate"].Value);

        var etaMatch = X26xLooseEtaRegex.Match(line);
        var eta = ParseEta(etaMatch.Groups["remainingeta"].Value)
            ?? ParseEta(etaMatch.Groups["eta"].Value);

        long? estimatedFileSizeBytes = null;
        var sizeMatch = X26xLooseSizeRegex.Match(line);
        if (sizeMatch.Success)
        {
            estimatedFileSizeBytes = ParseSizeToBytes(sizeMatch.Groups["size"].Value, sizeMatch.Groups["unit"].Value);
        }

        if (!estimatedFileSizeBytes.HasValue)
        {
            var bracketedSizeMatches = X26xBracketedSizeRegex.Matches(line);
            if (bracketedSizeMatches.Count > 0)
            {
                var lastSize = bracketedSizeMatches[bracketedSizeMatches.Count - 1];
                estimatedFileSizeBytes = ParseSizeToBytes(lastSize.Groups["size"].Value, lastSize.Groups["unit"].Value);
            }
        }

        if (!estimatedFileSizeBytes.HasValue && parsedTotalFrames is > 0 && sourceFramesPerSecond is > 0 && bitrate is > 0)
        {
            estimatedFileSizeBytes = EstimateFileSizeBytes(parsedTotalFrames.Value, sourceFramesPerSecond.Value, bitrate.Value);
        }

        var progressFraction = TryBuildProgressFraction(explicitPercent, currentFrame, parsedTotalFrames);

        if (!progressFraction.HasValue
            && !currentFrame.HasValue
            && !fps.HasValue
            && !bitrate.HasValue
            && !eta.HasValue
            && !estimatedFileSizeBytes.HasValue)
        {
            return null;
        }

        return new ParsedProgressSnapshot(
            progressFraction,
            new EncodingProgressSnapshot(currentFrame, parsedTotalFrames, fps, bitrate, eta, estimatedFileSizeBytes));
    }

    private static string NormalizeX26xProgressPrefix(string line)
    {
        if (line.StartsWith("x264 ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("x265 ", StringComparison.OrdinalIgnoreCase))
        {
            var bracketIndex = line.IndexOf('[');
            if (bracketIndex > 0)
            {
                return line[bracketIndex..].TrimStart();
            }
        }

        return line;
    }

    private static double ParseInvariantDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0.0;
    }

    private static double? ParseInvariantDoubleNullable(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ParseInvariantLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static TimeSpan? ParseEta(string value)
    {
        var normalized = value?.Trim().TrimStart('-');
        return TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out var eta)
            ? eta
            : null;
    }

    private static long? ParseSizeToBytes(string sizeValue, string unit)
    {
        if (string.IsNullOrWhiteSpace(sizeValue))
        {
            return null;
        }

        var size = ParseInvariantDouble(sizeValue);
        var multiplier = unit.Trim().ToUpperInvariant() switch
        {
            "KB" => 1024d,
            "MB" => 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            "TB" => 1024d * 1024d * 1024d * 1024d,
            _ => 1d
        };

        return (long)Math.Round(size * multiplier, MidpointRounding.AwayFromZero);
    }

    private static long EstimateFileSizeBytes(long totalFrames, double sourceFramesPerSecond, double bitrateKbps)
    {
        var durationSeconds = totalFrames / sourceFramesPerSecond;
        var bytes = durationSeconds * (bitrateKbps * 1000d / 8d);
        return (long)Math.Round(bytes, MidpointRounding.AwayFromZero);
    }

    private static double? TryBuildProgressFraction(double? explicitPercent, long? currentFrame, long? totalFrames)
    {
        if (explicitPercent.HasValue)
        {
            return Math.Clamp(explicitPercent.Value / 100.0, 0.0, 1.0);
        }

        if (currentFrame.HasValue && totalFrames is > 0)
        {
            return Math.Clamp(currentFrame.Value / (double)totalFrames.Value, 0.0, 1.0);
        }

        return null;
    }

    private static string GetAvailableLogPath(EncodingJobRequest request)
    {
        var outputPath = request.OutputPath;
        var directory = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        var baseName = Path.GetFileNameWithoutExtension(outputPath);
        var suffix = BuildLogFileSuffix(request.Profile);
        var extension = ".log";
        var candidate = Path.Combine(directory, $"{baseName}{suffix}{extension}");

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var index = 0; index < 10000; index++)
        {
            var timestampSuffix = index == 0
                ? $"_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"_{DateTime.Now:yyyyMMdd_HHmmss}_{index + 1}";
            candidate = Path.Combine(directory, $"{baseName}{suffix}{timestampSuffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{baseName}{suffix}_{Guid.NewGuid():N}{extension}");
    }

    private static string BuildLogFileSuffix(EncodingProfile profile)
    {
        var encoderToken = profile.Kind.ToShortName();

        var rateToken = profile.RateControl switch
        {
            RateControlMode.Crf => $"_crf{FormatFileTokenNumber(profile.Quality)}",
            RateControlMode.Cq => $"_cq{FormatFileTokenNumber(profile.Quality)}",
            RateControlMode.Qp => $"_qp{FormatFileTokenNumber(profile.Quality)}",
            RateControlMode.Abr => $"_abr{profile.Bitrate ?? 3500}",
            RateControlMode.Vbr => $"_vbr{profile.Bitrate ?? 3500}",
            RateControlMode.TwoPass => $"_2pass{profile.Bitrate ?? 3500}",
            _ => string.Empty
        };

        return $"_{encoderToken}{rateToken}";
    }

    private static string FormatFileTokenNumber(double value)
    {
        return value
            .ToString("0.0##", CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.')
            .Replace('.', '_');
    }

    private static string LastMeaningfulLine(string log)
    {
        return log
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(static line => !string.IsNullOrWhiteSpace(line))
            ?? string.Empty;
    }

    private static Process CreateProcess(EncodingExecutionStep step, string encoderPath)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = step.FileName,
                Arguments = step.Arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(encoderPath) ?? AppContext.BaseDirectory
            },
            EnableRaisingEvents = true
        };
    }

    private void TryTerminateProcess(Guid jobId, Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
                process.WaitForExit(2000);
            }
        }
        catch
        {
        }
        finally
        {
            _activeProcesses.TryRemove(jobId, out _);
        }
    }

    private static ParsedProgressSnapshot? ApplyStageProgress(ParsedProgressSnapshot? progressSnapshot, EncodingExecutionStep step)
    {
        if (progressSnapshot is null)
        {
            return null;
        }

        var overallProgress = progressSnapshot.ProgressFraction.HasValue
            ? Math.Clamp(((step.StageIndex - 1) + progressSnapshot.ProgressFraction.Value) / step.StageCount, 0.0, 1.0)
            : (double?)null;

        return progressSnapshot with { ProgressFraction = overallProgress };
    }

    private static double BuildStageStartingProgress(EncodingExecutionStep step)
    {
        return step.StageCount <= 1
            ? 0.0
            : Math.Clamp((step.StageIndex - 1) / (double)step.StageCount, 0.0, 1.0);
    }

    private static double BuildStageFailureProgress(EncodingExecutionStep step)
    {
        return step.StageCount <= 1
            ? 0.0
            : Math.Clamp((step.StageIndex - 1) / (double)step.StageCount, 0.0, 1.0);
    }

    private static string BuildStageStartingSummary(EncodingExecutionStep step)
    {
        return step.StageCount > 1
            ? $"开始第 {step.StageIndex}/{step.StageCount} 遍"
            : "编码已启动";
    }

    private static string BuildRunningSummary(EncodingExecutionStep step, double? progressFraction)
    {
        if (step.StageCount > 1)
        {
            return $"第 {step.StageIndex}/{step.StageCount} 遍编码中";
        }

        return progressFraction is { } progressValue ? $"编码中 {progressValue:P0}" : "编码中";
    }

    private static string BuildStageStartingDetail(EncodingExecutionStep step)
    {
        return step.StageCount > 1
            ? $"开始执行第 {step.StageIndex}/{step.StageCount} 遍。"
            : "开始执行编码任务。";
    }

    private static EncodingProgressSnapshot? BuildStageStartingSnapshot(EncodingExecutionPlan plan, EncodingExecutionStep step)
    {
        if (plan.TotalFrames is null)
        {
            return null;
        }

        return new EncodingProgressSnapshot(0, plan.TotalFrames, null, null, null, null);
    }

    private static void AppendStageHeader(EncodingExecutionStep step, StreamWriter rawLogWriter, StringBuilder visibleLogBuilder)
    {
        if (step.StageCount <= 1)
        {
            return;
        }

        AppendLogLine(rawLogWriter, $"--- PASS {step.StageIndex}/{step.StageCount} ---");
        AppendLogLine(rawLogWriter, step.DisplayCommand);
        AppendLogLine(visibleLogBuilder, $"--- PASS {step.StageIndex}/{step.StageCount} ---");
        AppendLogLine(visibleLogBuilder, step.DisplayCommand);
    }

    private static void AppendLogLine(StreamWriter writer, string line)
    {
        writer.WriteLine(line);
        writer.WriteLine();
    }

    private static void AppendLogLine(StringBuilder builder, string line)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(line);
    }

    private string CreateTemporaryRawLogPath(EncodingJobRequest request)
    {
        var directory = Path.Combine(GetJobTempDirectory(request), "logs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{request.JobId:N}.raw.log");
    }

    private static StreamWriter CreateRawLogWriter(string path)
    {
        var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        return new StreamWriter(stream, Encoding.UTF8);
    }

    private static void CleanupTemporaryRawLog(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void CleanupPlanArtifacts(EncodingExecutionPlan plan)
    {
        if (plan.CleanupPaths is null)
        {
            return;
        }

        foreach (var path in plan.CleanupPaths.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private string BuildMultipassStatsPath(EncodingJobRequest request, EncoderKind kind)
    {
        var directory = Path.Combine(GetJobTempDirectory(request), "multipass");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{request.JobId:N}_{kind.ToShortName()}_stats.log");
    }

    private static string GetJobTempDirectory(EncodingJobRequest request)
    {
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        var baseDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.CurrentDirectory
            : outputDirectory;
        return Path.Combine(baseDirectory, TempWorkspaceFolderName, request.JobId.ToString("N"));
    }

    private static void CleanupJobTempDirectory(EncodingJobRequest request)
    {
        var jobTempDirectory = GetJobTempDirectory(request);
        TryDeleteDirectoryIfEmpty(jobTempDirectory);

        var rootDirectory = Path.GetDirectoryName(jobTempDirectory);
        TryDeleteDirectoryIfEmpty(rootDirectory);
    }

    private static void TryDeleteDirectoryIfEmpty(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, false);
            }
        }
        catch
        {
        }
    }

    private static string Optional(string name, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $"{name} {value}";
    }

    private static string QuoteIfPresent(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : Quote(value);
    }

    private static InputPipelineKind ResolvePipelineKind(EncodingJobRequest request)
    {
        if (request.PipelineKind != InputPipelineKind.Auto)
        {
            return request.PipelineKind;
        }

        return InputSourceSupport.ResolvePipelineKind(request.SourcePath);
    }

    private static void ValidateShellPipelineArguments(EncodingJobRequest request, InputPipelineKind pipelineKind)
    {
        if (pipelineKind is InputPipelineKind.Y4mFile or InputPipelineKind.RawYuvFile)
        {
            return;
        }

        ThrowIfContainsForbiddenShellCharacters(request.Profile.AdditionalArguments, "自定义压制参数");

        if (request.Profile.Kind == EncoderKind.X265)
        {
            ThrowIfContainsForbiddenShellCharacters(request.Profile.UhdParameters, "x265 UHD / HDR 附加参数");
        }
    }

    private static void ThrowIfContainsForbiddenShellCharacters(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.IndexOfAny(ForbiddenCmdCharacters) >= 0
            || value.Contains('\r')
            || value.Contains('\n'))
        {
            throw new InvalidOperationException($"{parameterName} 中包含不受支持的命令行控制字符。为避免命令注入风险，自动编码仅允许普通参数文本。请移除 & | < > ^ % 和换行后重试。");
        }
    }

    private static string X264InputSwitch(InputPipelineKind pipelineKind)
    {
        return pipelineKind switch
        {
            InputPipelineKind.Y4mFile => "--demuxer y4m",
            InputPipelineKind.RawYuvFile => "--demuxer raw",
            _ => "--demuxer y4m --stdin y4m"
        };
    }

    private static string BuildX264DirectInputArguments(string sourcePath, InputPipelineKind pipelineKind)
    {
        return pipelineKind switch
        {
            InputPipelineKind.Y4mFile => $"{X264InputSwitch(pipelineKind)} {Quote(sourcePath)}",
            InputPipelineKind.RawYuvFile => $"{X264InputSwitch(pipelineKind)} {Quote(sourcePath)}",
            _ => $"{X264InputSwitch(pipelineKind)} -"
        };
    }

    private static string BuildX265DirectInputArguments(string sourcePath, InputPipelineKind pipelineKind)
    {
        return pipelineKind switch
        {
            InputPipelineKind.Y4mFile => $"--y4m --input {Quote(sourcePath)}",
            InputPipelineKind.RawYuvFile => $"--input {Quote(sourcePath)}",
            _ => "--y4m --input -"
        };
    }

    private static string BuildSvtDirectInputArguments(string sourcePath, InputPipelineKind pipelineKind)
    {
        return pipelineKind switch
        {
            InputPipelineKind.Y4mFile => $"--input {Quote(sourcePath)}",
            InputPipelineKind.RawYuvFile => $"--input {Quote(sourcePath)}",
            _ => "--input -"
        };
    }

    private static string OptionalSegment(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static string JoinArguments(params string[] parts)
    {
        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.0##", CultureInfo.InvariantCulture);
    }

    private sealed record ParsedProgressSnapshot(
        double? ProgressFraction,
        EncodingProgressSnapshot? Snapshot);

    private sealed record ProgressDispatchState(
        DateTimeOffset LastReportedAt,
        double? LastProgressFraction,
        long? LastCurrentFrame);
}
