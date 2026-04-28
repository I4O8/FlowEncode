using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class Av1anAutoCompressionRunner : IAutoCompressionRunner
{
    private const string TempWorkspaceFolderName = ".flowencode-temp";
    private static readonly Regex ProgressRegex = new(@"(?<!\d)(?<value>\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);
    private static readonly Regex FractionProgressRegex = new(@"(?<!\d)(?<done>\d{1,7})\s*/\s*(?<total>\d{1,7})(?!\d)", RegexOptions.Compiled);
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(250);

    private readonly ExternalToolLocator _toolLocator;
    private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

    public Av1anAutoCompressionRunner(LocalAppPaths paths, IAppSettingsService settingsService)
    {
        _toolLocator = new ExternalToolLocator(paths, settingsService);
    }

    public string BuildDisplayCommand(AutoCompressionRequest request)
    {
        var av1anPath = _toolLocator.ResolveAv1an();
        var arguments = BuildArguments(request, GetTempDirectory(request));
        return $"{Quote(av1anPath)} {arguments}";
    }

    public void Abort(Guid jobId)
    {
        if (_activeProcesses.TryGetValue(jobId, out var process))
        {
            TryTerminateProcess(jobId, process);
        }
    }

    public async Task<AutoCompressionResult> RunAsync(
        AutoCompressionRequest request,
        IProgress<AutoCompressionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException("未找到自动压制输入源文件。", request.SourcePath);
        }

        var av1anPath = _toolLocator.ResolveAv1an();
        await EnsureAv1anRuntimeReadyAsync(av1anPath, cancellationToken);
        var tempDirectory = GetTempDirectory(request);
        Directory.CreateDirectory(tempDirectory);
        var arguments = BuildArguments(request, tempDirectory);
        var displayCommand = $"{Quote(av1anPath)} {arguments}";
        var logBuilder = new StringBuilder();
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        progress?.Report(new AutoCompressionProgress(
            request.JobId,
            EncodingJobState.Running,
            null,
            "自动压制已启动",
            displayCommand));

        var gate = new object();
        var lastProgress = 0.0;
        var hasKnownProgress = false;
        var lastReportAt = DateTimeOffset.MinValue;

        void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            AutoCompressionProgress? update = null;

            lock (gate)
            {
                logBuilder.AppendLine(line);

                var now = DateTimeOffset.UtcNow;
                var parsedProgress = ParseProgressFraction(line) ?? ParseFractionProgress(line);
                if (parsedProgress.HasValue)
                {
                    hasKnownProgress = true;
                    lastProgress = Math.Clamp(Math.Max(lastProgress, parsedProgress.Value), 0.0, 1.0);
                }

                var shouldReport = parsedProgress.HasValue
                    || now - lastReportAt >= ProgressReportInterval;

                if (!shouldReport)
                {
                    return;
                }

                lastReportAt = now;
                var summary = BuildRunningSummary(lastProgress, line);
                update = new AutoCompressionProgress(
                    request.JobId,
                    EncodingJobState.Running,
                    hasKnownProgress ? lastProgress : null,
                    summary,
                    line);
            }

            if (update is not null)
            {
                progress?.Report(update);
            }
        }

        Process? process = null;
        Task pumpOutput = Task.CompletedTask;
        Task pumpError = Task.CompletedTask;

        try
        {
            process = CreateProcess(av1anPath, arguments);
            process.Start();
            _activeProcesses[request.JobId] = process;

            pumpOutput = PumpAsync(process.StandardOutput, HandleLine, cancellationToken);
            pumpError = PumpAsync(process.StandardError, HandleLine, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(pumpOutput, pumpError);

            _activeProcesses.TryRemove(request.JobId, out _);

            var log = logBuilder.ToString();
            if (process.ExitCode == 0)
            {
                progress?.Report(new AutoCompressionProgress(
                    request.JobId,
                    EncodingJobState.Completed,
                    1.0,
                    "自动压制完成",
                    LastMeaningfulLine(log)));

                return new AutoCompressionResult(
                    request.JobId,
                    EncodingJobState.Completed,
                    0,
                    "自动压制完成",
                    log,
                    displayCommand);
            }

            progress?.Report(new AutoCompressionProgress(
                request.JobId,
                EncodingJobState.Failed,
                null,
                $"自动压制失败，退出代码 {process.ExitCode}",
                LastMeaningfulLine(log)));

            return new AutoCompressionResult(
                request.JobId,
                EncodingJobState.Failed,
                process.ExitCode,
                $"自动压制失败，退出代码 {process.ExitCode}",
                log,
                displayCommand);
        }
        catch (OperationCanceledException)
        {
            if (process is not null && !process.HasExited)
            {
                TryTerminateProcess(request.JobId, process);
            }

            try
            {
                await Task.WhenAll(pumpOutput, pumpError);
            }
            catch
            {
            }

            var log = logBuilder.ToString();
            progress?.Report(new AutoCompressionProgress(
                request.JobId,
                EncodingJobState.Cancelled,
                null,
                "自动压制已取消",
                "任务已取消。"));

            return new AutoCompressionResult(
                request.JobId,
                EncodingJobState.Cancelled,
                -1,
                "自动压制已取消",
                log,
                displayCommand);
        }
        finally
        {
            _activeProcesses.TryRemove(request.JobId, out _);
            CleanupJobTempDirectory(request);
        }
    }

    private string BuildArguments(AutoCompressionRequest request, string tempDirectory)
    {
        if (request.Probes <= 0)
        {
            throw new InvalidOperationException("探测次数必须大于 0。");
        }

        if (request.TargetVmaf <= 0 || request.TargetVmaf > 100)
        {
            throw new InvalidOperationException("目标 VMAF 必须在 0 到 100 之间。");
        }

        if (!string.IsNullOrWhiteSpace(request.VideoParameters)
            && (request.VideoParameters.Contains('\r') || request.VideoParameters.Contains('\n')))
        {
            throw new InvalidOperationException("小参不支持换行，请使用单行参数。");
        }

        var args = new List<string>
        {
            $"-i {Quote(request.SourcePath)}",
            $"-o {Quote(request.OutputPath)}",
            "-y",
            $"--temp {Quote(tempDirectory)}",
            $"--encoder {MapEncoder(request.EncoderKind)}",
            "--target-metric vmaf",
            $"--target-quality {request.TargetVmaf.ToString("0.###", CultureInfo.InvariantCulture)}",
            $"--probes {request.Probes.ToString(CultureInfo.InvariantCulture)}"
        };

        if (request.Workers is > 0)
        {
            args.Add($"--workers {request.Workers.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(request.VideoParameters))
        {
            args.Add($"--video-params {Quote(request.VideoParameters.Trim())}");
        }

        return string.Join(" ", args);
    }

    private static string GetTempDirectory(AutoCompressionRequest request)
    {
        var outputDirectory = Path.GetDirectoryName(request.OutputPath);
        var baseDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.CurrentDirectory
            : outputDirectory;
        return Path.Combine(baseDirectory, TempWorkspaceFolderName, "av1an", request.JobId.ToString("N"));
    }

    private static void CleanupJobTempDirectory(AutoCompressionRequest request)
    {
        var jobTempDirectory = GetTempDirectory(request);
        TryDeleteDirectoryIfEmpty(jobTempDirectory);
        TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(jobTempDirectory));
        TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(Path.GetDirectoryName(jobTempDirectory)));
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

    private static string MapEncoder(EncoderKind kind)
    {
        return kind switch
        {
            EncoderKind.X264 => "x264",
            EncoderKind.X265 => "x265",
            EncoderKind.SvtAv1 => "svt-av1",
            _ => throw new InvalidOperationException($"不支持的编码器: {kind}")
        };
    }

    private static Process CreateProcess(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(fileName) ?? AppContext.BaseDirectory
        };
        VapourSynthRuntimePathResolver.EnrichProcessPath(startInfo);

        return new Process
        {
            StartInfo = startInfo
        };
    }

    private static async Task PumpAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            onLine(line);
        }
    }

    private static double? ParseProgressFraction(string line)
    {
        var match = ProgressRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
        {
            return null;
        }

        return Math.Clamp(percent / 100.0, 0.0, 1.0);
    }

    private static double? ParseFractionProgress(string line)
    {
        if (!LooksLikeProgressLine(line))
        {
            return null;
        }

        var match = FractionProgressRegex.Match(line);
        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(match.Groups["done"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var done)
            || !int.TryParse(match.Groups["total"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total)
            || total <= 0
            || done < 0)
        {
            return null;
        }

        if (done > total)
        {
            return null;
        }

        return Math.Clamp((double)done / total, 0.0, 1.0);
    }

    private static bool LooksLikeProgressLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return line.Contains("chunk", StringComparison.OrdinalIgnoreCase)
            || line.Contains("scene", StringComparison.OrdinalIgnoreCase)
            || line.Contains("probe", StringComparison.OrdinalIgnoreCase)
            || line.Contains("target quality", StringComparison.OrdinalIgnoreCase)
            || line.Contains("split", StringComparison.OrdinalIgnoreCase)
            || line.Contains("encode", StringComparison.OrdinalIgnoreCase)
            || line.Contains("progress", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRunningSummary(double progressFraction, string line)
    {
        var stageText = ResolveStageText(line);
        if (progressFraction > 0)
        {
            return string.IsNullOrWhiteSpace(stageText)
                ? $"自动压制中 {progressFraction:P0}"
                : $"{stageText} {progressFraction:P0}";
        }

        return stageText ?? "自动压制中";
    }

    private static string? ResolveStageText(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (line.Contains("scenecut", StringComparison.OrdinalIgnoreCase)
            || line.Contains("scene(s)", StringComparison.OrdinalIgnoreCase))
        {
            return "场景检测中";
        }

        if (line.Contains("target quality", StringComparison.OrdinalIgnoreCase)
            || line.Contains("probe", StringComparison.OrdinalIgnoreCase)
            || line.Contains("vmaf", StringComparison.OrdinalIgnoreCase))
        {
            return "VMAF 探测中";
        }

        if (line.Contains("chunk", StringComparison.OrdinalIgnoreCase)
            || line.Contains("encoding", StringComparison.OrdinalIgnoreCase)
            || line.Contains("fps", StringComparison.OrdinalIgnoreCase))
        {
            return "编码中";
        }

        if (line.Contains("concat", StringComparison.OrdinalIgnoreCase)
            || line.Contains("merge", StringComparison.OrdinalIgnoreCase)
            || line.Contains("mux", StringComparison.OrdinalIgnoreCase))
        {
            return "封装中";
        }

        if (line.Contains("input:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("split", StringComparison.OrdinalIgnoreCase))
        {
            return "预处理中";
        }

        return null;
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

    private static string LastMeaningfulLine(string log)
    {
        return log
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(static line => !string.IsNullOrWhiteSpace(line))
            ?? string.Empty;
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    private async Task EnsureAv1anRuntimeReadyAsync(string av1anPath, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(av1anPath, "--version");
        using (ErrorDialogSuppression.Enter())
        {
            process.Start();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode == 0)
        {
            return;
        }

        var message = BuildRuntimeFailureMessage(process.ExitCode, output, error);
        throw new InvalidOperationException(message);
    }

    private static string BuildRuntimeFailureMessage(int exitCode, string output, string error)
    {
        var stderr = (error ?? string.Empty).Trim();
        var stdout = (output ?? string.Empty).Trim();
        var merged = string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : string.IsNullOrWhiteSpace(stdout)
                ? stderr
                : $"{stderr}{Environment.NewLine}{stdout}";

        if (exitCode == unchecked((int)0xC0000135) || exitCode == -1073741515)
        {
            return "Av1an 启动失败：缺少运行时依赖（常见为 VSScript.dll / VapourSynth 运行时）。请安装或修复 VapourSynth 后重试。";
        }

        if (merged.Contains("Failed to get VSScript API", StringComparison.OrdinalIgnoreCase))
        {
            return "Av1an 启动失败：检测到 VSScript API 不可用。当前 VapourSynth 运行时与 Av1an 不兼容或安装损坏，请重装 VapourSynth（并确认 vspipe 可正常运行）后重试。";
        }

        if (!string.IsNullOrWhiteSpace(merged))
        {
            return $"Av1an 预检失败（退出码 {exitCode}）：{merged}";
        }

        return $"Av1an 预检失败，退出码 {exitCode}。";
    }
}
