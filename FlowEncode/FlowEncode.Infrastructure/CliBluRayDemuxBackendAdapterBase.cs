using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public abstract class CliBluRayDemuxBackendAdapterBase : IBluRayDemuxBackendAdapter
{
    private const int MaxLogLength = 240_000;
    private readonly IToolProbeService _toolProbeService;
    private readonly ConcurrentDictionary<Guid, Process> _activeProcesses = new();

    protected CliBluRayDemuxBackendAdapterBase(IToolProbeService toolProbeService)
    {
        _toolProbeService = toolProbeService;
    }

    public abstract BluRayDemuxBackend Backend { get; }

    public abstract Task<IReadOnlyList<BluRayPlaylistItem>> ScanDiscAsync(
        string discPath,
        CancellationToken cancellationToken = default);

    public abstract Task<BluRayPlaylistScanResult> ScanPlaylistAsync(
        string discPath,
        BluRayPlaylistItem playlist,
        CancellationToken cancellationToken = default);

    public abstract Task<BluRayDemuxResult> RunAsync(
        BluRayDemuxRequest request,
        IProgress<BluRayDemuxProgress>? progress = null,
        CancellationToken cancellationToken = default);

    public abstract string BuildDisplayCommand(BluRayDemuxRequest request);

    public void Abort(Guid jobId)
    {
        if (_activeProcesses.TryGetValue(jobId, out var process))
        {
            TryKillProcess(process);
        }
    }

    protected async Task<string> ResolveToolPathAsync(RegisteredToolKind kind, CancellationToken cancellationToken)
    {
        var result = await _toolProbeService.ProbeAsync(kind, cancellationToken);
        if (result.IsReady && !string.IsNullOrWhiteSpace(result.ExecutablePath))
        {
            return result.ExecutablePath;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.FailureReason)
            ? $"未找到可用的 {kind.ToDisplayName()}。"
            : result.FailureReason);
    }

    protected static ProcessStartInfo CreateStartInfo(string executablePath, string? workingDirectory = null)
    {
        return new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
                ?? Path.GetDirectoryName(executablePath)
                ?? AppContext.BaseDirectory
        };
    }

    protected async Task<ProcessCaptureResult> CaptureProcessAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        var outputBuilder = new StringBuilder();

        using var process = new Process
        {
            StartInfo = startInfo
        };

        using var registration = cancellationToken.Register(() => TryKillProcess(process));

        process.Start();
        using var processJob = ProcessJobObject.TryAttach(process);
        var stdOutTask = ReadLinesAsync(process.StandardOutput, outputBuilder, null, cancellationToken);
        var stdErrTask = ReadLinesAsync(process.StandardError, outputBuilder, null, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            processJob?.Terminate();
            TryKillProcess(process);
            throw;
        }

        return new ProcessCaptureResult(process.ExitCode, outputBuilder.ToString().Trim());
    }

    protected async Task<BluRayDemuxResult> RunProcessAsync(
        BluRayDemuxRequest request,
        string displayCommand,
        ProcessStartInfo startInfo,
        Func<string, double?> progressParser,
        Func<string, bool>? successLineDetector,
        string startSummary,
        string completedSummary,
        string cancelledSummary,
        string failedSummary,
        IProgress<BluRayDemuxProgress>? progress,
        CancellationToken cancellationToken)
    {
        var logBuilder = new StringBuilder();
        var gate = new object();
        var lastReportedProgress = 0.0;
        var hasKnownProgress = false;
        var lastDetailLine = string.Empty;

        void HandleLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (logBuilder.Length < MaxLogLength)
            {
                logBuilder.AppendLine(line);
            }

            lock (gate)
            {
                lastDetailLine = line;
                var parsedProgress = progressParser(line);
                if (parsedProgress.HasValue)
                {
                    hasKnownProgress = true;
                    lastReportedProgress = Math.Max(lastReportedProgress, Math.Clamp(parsedProgress.Value, 0.0, 1.0));
                }
            }

            progress?.Report(new BluRayDemuxProgress(
                request.JobId,
                EncodingJobState.Running,
                hasKnownProgress ? lastReportedProgress : null,
                hasKnownProgress ? $"{startSummary} {lastReportedProgress * 100:0.#}%" : startSummary,
                line));
        }

        Process? process = null;
        ProcessJobObject? processJob = null;
        Task pumpOutput = Task.CompletedTask;
        Task pumpError = Task.CompletedTask;
        var exitCode = -1;
        var hasExitCode = false;

        progress?.Report(new BluRayDemuxProgress(
            request.JobId,
            EncodingJobState.Running,
            null,
            startSummary,
            string.Empty));

        try
        {
            process = new Process
            {
                StartInfo = startInfo
            };

            _activeProcesses[request.JobId] = process;

            using var registration = cancellationToken.Register(() => TryKillProcess(process));

            process.Start();
            processJob = ProcessJobObject.TryAttach(process);
            pumpOutput = ReadLinesAsync(process.StandardOutput, null, HandleLine, cancellationToken);
            pumpError = ReadLinesAsync(process.StandardError, null, HandleLine, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            processJob?.Terminate();
            await Task.WhenAll(pumpOutput, pumpError);
            hasExitCode = TryGetExitCode(process, out exitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            processJob?.Terminate();
            TryKillProcess(process);
            await Task.WhenAll(pumpOutput, pumpError);
            hasExitCode = TryGetExitCode(process, out exitCode);

            var cancelledLog = logBuilder.ToString().Trim();
            progress?.Report(new BluRayDemuxProgress(
                request.JobId,
                EncodingJobState.Cancelled,
                hasKnownProgress ? lastReportedProgress : null,
                cancelledSummary,
                lastDetailLine));

            return new BluRayDemuxResult(
                request.JobId,
                EncodingJobState.Cancelled,
                hasExitCode ? exitCode : -1,
                string.IsNullOrWhiteSpace(lastDetailLine) ? cancelledSummary : lastDetailLine,
                cancelledLog,
                displayCommand,
                request.Selections.Select(static selection => selection.OutputPath).ToList());
        }
        finally
        {
            if (process is not null)
            {
                _activeProcesses.TryRemove(request.JobId, out _);
                processJob?.Dispose();
                process.Dispose();
            }
        }

        var log = logBuilder.ToString().Trim();
        var lastMeaningfulLine = GetLastMeaningfulLine(log);
        var reportedSuccess = HasSuccessfulTerminalLine(log, successLineDetector);

        if ((hasExitCode && exitCode == 0) || (!hasExitCode && reportedSuccess))
        {
            progress?.Report(new BluRayDemuxProgress(
                request.JobId,
                EncodingJobState.Completed,
                1.0,
                completedSummary,
                lastMeaningfulLine));

            return new BluRayDemuxResult(
                request.JobId,
                EncodingJobState.Completed,
                hasExitCode ? exitCode : 0,
                string.IsNullOrWhiteSpace(lastMeaningfulLine) ? completedSummary : lastMeaningfulLine,
                log,
                displayCommand,
                request.Selections.Select(static selection => selection.OutputPath).ToList());
        }

        if (!hasExitCode)
        {
            var exitStateUnavailableDetail = string.IsNullOrWhiteSpace(lastMeaningfulLine)
                ? "无法读取进程退出状态。"
                : $"{lastMeaningfulLine}{Environment.NewLine}无法读取进程退出状态。";

            progress?.Report(new BluRayDemuxProgress(
                request.JobId,
                EncodingJobState.Failed,
                hasKnownProgress ? lastReportedProgress : null,
                failedSummary,
                exitStateUnavailableDetail));

            return new BluRayDemuxResult(
                request.JobId,
                EncodingJobState.Failed,
                -1,
                exitStateUnavailableDetail,
                log,
                displayCommand,
                request.Selections.Select(static selection => selection.OutputPath).ToList());
        }

        progress?.Report(new BluRayDemuxProgress(
            request.JobId,
            EncodingJobState.Failed,
            hasKnownProgress ? lastReportedProgress : null,
            failedSummary,
            lastMeaningfulLine));

        return new BluRayDemuxResult(
            request.JobId,
            EncodingJobState.Failed,
            exitCode,
            string.IsNullOrWhiteSpace(lastMeaningfulLine) ? failedSummary : lastMeaningfulLine,
            log,
            displayCommand,
            request.Selections.Select(static selection => selection.OutputPath).ToList());
    }

    protected static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        StringBuilder? sink,
        Action<string>? lineHandler,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (line is null)
            {
                break;
            }

            var normalized = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (sink is not null)
            {
                sink.AppendLine(normalized);
            }

            lineHandler?.Invoke(normalized);
        }
    }

    private static void TryKillProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }
    }

    private static bool TryGetExitCode(Process? process, out int exitCode)
    {
        exitCode = -1;
        if (process is null)
        {
            return false;
        }

        try
        {
            exitCode = process.ExitCode;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool HasSuccessfulTerminalLine(string log, Func<string, bool>? successLineDetector)
    {
        if (successLineDetector is null || string.IsNullOrWhiteSpace(log))
        {
            return false;
        }

        var lastMeaningfulLine = GetLastMeaningfulLine(log);
        return !string.IsNullOrWhiteSpace(lastMeaningfulLine) && successLineDetector(lastMeaningfulLine);
    }

    private static string GetLastMeaningfulLine(string log)
    {
        if (string.IsNullOrWhiteSpace(log))
        {
            return string.Empty;
        }

        return log
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(static line => !string.IsNullOrWhiteSpace(line))
            ?? string.Empty;
    }

    protected sealed record ProcessCaptureResult(int ExitCode, string Output);
}
