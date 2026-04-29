using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class VapourSynthPreviewService : IVapourSynthPreviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IToolProbeService _toolProbeService;
    private readonly string _sessionRootPath;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _pythonPathLock = new(1, 1);
    private readonly TimeSpan _closeTimeout = TimeSpan.FromSeconds(2);
    private readonly StringBuilder _stderrBuffer = new();
    private string? _pythonPath;
    private Process? _hostProcess;
    private StreamWriter? _hostInputWriter;
    private StreamReader? _hostOutputReader;
    private string? _activeSessionPath;
    private int _commandCounter;
    private bool _stderrTracebackActive;
    private bool _disposed;

    public event EventHandler<VapourSynthPreviewLogEventArgs>? LogEmitted;

    public VapourSynthPreviewService(
        IToolProbeService toolProbeService,
        LocalAppPaths appPaths)
    {
        _toolProbeService = toolProbeService;
        _sessionRootPath = Path.Combine(appPaths.DataRootPath, "vapoursynth-preview");
        Directory.CreateDirectory(_sessionRootPath);
    }

    public async Task<VapourSynthPreviewSessionInfo> OpenSessionAsync(
        VapourSynthPreviewOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            var normalizedSourceFilePath = NormalizeAbsolutePath(request.SourceFilePath);
            var normalizedWorkingDirectory = NormalizeWorkingDirectory(request.WorkingDirectory, normalizedSourceFilePath);

            await CloseHostCoreAsync();

            var sessionPath = CreateSessionDirectory();
            _activeSessionPath = sessionPath;

            var startupPath = Path.Combine(sessionPath, "startup.json");
            var startupPayload = new StartupRequestDto(
                normalizedSourceFilePath,
                request.DisplayName,
                request.Content ?? string.Empty,
                normalizedWorkingDirectory);
            var startupJson = JsonSerializer.Serialize(startupPayload, JsonOptions);
            await File.WriteAllTextAsync(startupPath, startupJson, new UTF8Encoding(false), cancellationToken);

            var pythonPath = await ResolvePythonPathAsync(cancellationToken);
            var helperPath = GetHelperPath();

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = normalizedWorkingDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            PythonProcessStartInfoHelper.ApplyUtf8(startInfo);
            startInfo.ArgumentList.Add(helperPath);
            startInfo.ArgumentList.Add(startupPath);
            VapourSynthRuntimePathResolver.EnrichProcessPath(startInfo);

            _hostProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _hostProcess.ErrorDataReceived += HostProcess_ErrorDataReceived;

            using var _ = ErrorDialogSuppression.Enter();
            _hostProcess.Start();
            _hostProcess.BeginErrorReadLine();

            _hostInputWriter = _hostProcess.StandardInput;
            _hostInputWriter.AutoFlush = true;
            _hostOutputReader = _hostProcess.StandardOutput;

            var response = await ReadResponseAsync(cancellationToken);
            if (!string.Equals(response.Type, "ready", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(GetResponseErrorMessage(response)
                    ?? "Preview helper did not finish initialization.");
            }

            var outputs = response.Outputs?
                .Select(static output => new VapourSynthPreviewOutputInfo(
                    output.Index,
                    string.IsNullOrWhiteSpace(output.Name) ? $"Output {output.Index}" : output.Name,
                    output.Width,
                    output.Height,
                    output.TotalFrames,
                    output.FpsNumerator,
                    output.FpsDenominator,
                    string.IsNullOrWhiteSpace(output.FormatName) ? "Unknown" : output.FormatName,
                    output.BitsPerSample))
                .OrderBy(static output => output.Index)
                .ToArray()
                ?? [];

            if (outputs.Length == 0)
            {
                throw new InvalidOperationException("The script did not expose any video outputs.");
            }

            return new VapourSynthPreviewSessionInfo(outputs);
        }
        catch (OperationCanceledException)
        {
            await CloseHostCoreAsync();
            throw;
        }
        catch (Exception)
        {
            await CloseHostCoreAsync();
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<VapourSynthPreviewFrameData> RenderFrameAsync(
        int outputIndex,
        int frameNumber,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            ThrowIfDisposed();
            if (_hostProcess is null
                || _hostInputWriter is null
                || _hostOutputReader is null
                || string.IsNullOrWhiteSpace(_activeSessionPath))
            {
                throw new InvalidOperationException("Preview session is not open.");
            }

            if (_hostProcess.HasExited)
            {
                throw new InvalidOperationException(BuildHostFailureMessage(
                    "Preview helper exited unexpectedly before rendering a frame."));
            }

            var requestId = Interlocked.Increment(ref _commandCounter);
            var rawPath = Path.Combine(_activeSessionPath, $"frame-{requestId:D6}.bgra");
            var command = new FrameCommandDto("renderFrame", requestId, outputIndex, frameNumber, rawPath);
            var commandJson = JsonSerializer.Serialize(command, JsonOptions);
            await _hostInputWriter.WriteLineAsync(commandJson);

            var response = await ReadResponseAsync(cancellationToken);
            if (!string.Equals(response.Type, "frame", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(GetResponseErrorMessage(response)
                    ?? "Preview helper returned an invalid frame response.");
            }

            if (response.RequestId != requestId)
            {
                throw new InvalidOperationException("Preview helper returned a mismatched frame response.");
            }

            if (string.IsNullOrWhiteSpace(response.RawPixelPath) || !File.Exists(response.RawPixelPath))
            {
                throw new InvalidOperationException("Preview helper did not produce a frame buffer.");
            }

            var properties = response.Properties?
                .Select(static item => new VapourSynthPreviewFrameProperty(
                    item.Key ?? string.Empty,
                    item.Value ?? string.Empty))
                .ToArray()
                ?? [];

            return new VapourSynthPreviewFrameData(
                response.OutputIndex,
                response.FrameNumber,
                response.Width,
                response.Height,
                response.RawPixelPath,
                response.FrameType,
                properties);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task CloseSessionAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);

        try
        {
            await CloseHostCoreAsync();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseHostCoreAsync().GetAwaiter().GetResult();
        _stateLock.Dispose();
        _pythonPathLock.Dispose();
    }

    private async Task<string> ResolvePythonPathAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_pythonPath))
        {
            return _pythonPath;
        }

        await _pythonPathLock.WaitAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_pythonPath))
            {
                return _pythonPath;
            }

            var probe = await _toolProbeService.ProbeAsync(RegisteredToolKind.Python, cancellationToken);
            if (!probe.IsReady || string.IsNullOrWhiteSpace(probe.ExecutablePath))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(probe.FailureReason)
                    ? "Python runtime is unavailable."
                    : probe.FailureReason);
            }

            _pythonPath = probe.ExecutablePath;
            return _pythonPath;
        }
        finally
        {
            _pythonPathLock.Release();
        }
    }

    private static string GetHelperPath()
    {
        var helperPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "VapourSynthEditor",
            "python",
            "preview_host.py");

        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException("VapourSynth preview helper was not found.", helperPath);
        }

        return helperPath;
    }

    private void HostProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        lock (_stderrBuffer)
        {
            if (_stderrBuffer.Length > 0)
            {
                _stderrBuffer.AppendLine();
            }

            _stderrBuffer.Append(e.Data);
        }

        EmitLog(
            ClassifyHelperStderrLine(e.Data),
            "helper",
            e.Data);
    }

    private async Task<HostResponseDto> ReadResponseAsync(CancellationToken cancellationToken)
    {
        if (_hostOutputReader is null || _hostProcess is null)
        {
            throw new InvalidOperationException("Preview helper output is unavailable.");
        }

        while (true)
        {
            var line = await _hostOutputReader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidOperationException(BuildHostFailureMessage(
                    "Preview helper returned no response."));
            }

            var response = JsonSerializer.Deserialize<HostResponseDto>(line, JsonOptions);
            if (response is null)
            {
                throw new InvalidOperationException("Preview helper returned invalid JSON.");
            }

            if (string.Equals(response.Type, "log", StringComparison.OrdinalIgnoreCase))
            {
                EmitLog(
                    MapLogLevel(response.Level),
                    string.IsNullOrWhiteSpace(response.Source) ? "helper" : response.Source,
                    response.Message ?? string.Empty);
                continue;
            }

            return response;
        }
    }

    private async Task CloseHostCoreAsync()
    {
        var process = _hostProcess;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    if (_hostInputWriter is not null)
                    {
                        var closeJson = JsonSerializer.Serialize(new CloseCommandDto("close"), JsonOptions);
                        await _hostInputWriter.WriteLineAsync(closeJson);
                    }

                    using var timeoutCancellationTokenSource = new CancellationTokenSource(_closeTimeout);
                    await process.WaitForExitAsync(timeoutCancellationTokenSource.Token);
                }
            }
            catch
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(CancellationToken.None);
                    }
                }
                catch
                {
                }
            }

            process.ErrorDataReceived -= HostProcess_ErrorDataReceived;
            process.Dispose();
        }

        _hostInputWriter?.Dispose();
        _hostOutputReader?.Dispose();
        _hostInputWriter = null;
        _hostOutputReader = null;
        _hostProcess = null;
        _commandCounter = 0;
        _stderrTracebackActive = false;

        if (!string.IsNullOrWhiteSpace(_activeSessionPath))
        {
            TryDeleteDirectory(_activeSessionPath);
            _activeSessionPath = null;
        }

        lock (_stderrBuffer)
        {
            _stderrBuffer.Clear();
        }
    }

    private string CreateSessionDirectory()
    {
        var sessionPath = Path.Combine(_sessionRootPath, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionPath);
        return sessionPath;
    }

    private static string NormalizeWorkingDirectory(string? workingDirectory, string? sourceFilePath)
    {
        var normalizedWorkingDirectory = NormalizeAbsolutePath(workingDirectory);
        if (!string.IsNullOrWhiteSpace(normalizedWorkingDirectory) && Directory.Exists(normalizedWorkingDirectory))
        {
            return normalizedWorkingDirectory;
        }

        var sourceDirectory = !string.IsNullOrWhiteSpace(sourceFilePath)
            ? Path.GetDirectoryName(sourceFilePath)
            : null;
        if (!string.IsNullOrWhiteSpace(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            return sourceDirectory;
        }

        return AppContext.BaseDirectory;
    }

    private static string? NormalizeAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private string BuildHostFailureMessage(string fallbackMessage)
    {
        lock (_stderrBuffer)
        {
            return _stderrBuffer.Length == 0
                ? fallbackMessage
                : $"{fallbackMessage} {_stderrBuffer}";
        }
    }

    private static string? GetResponseErrorMessage(HostResponseDto response)
    {
        return string.IsNullOrWhiteSpace(response.Message)
            ? null
            : response.Message;
    }

    private void EmitLog(
        VapourSynthPreviewLogLevel level,
        string source,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var normalizedMessage = message
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var normalizedSource = string.IsNullOrWhiteSpace(source) ? "preview" : source.Trim();

        foreach (var line in normalizedMessage.Split('\n'))
        {
            var trimmedLine = ConsoleOutputLineNormalizer.Normalize(line);
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            LogEmitted?.Invoke(
                this,
                new VapourSynthPreviewLogEventArgs(
                    new VapourSynthPreviewLogEntry(
                        DateTimeOffset.Now,
                        level,
                        normalizedSource,
                        trimmedLine)));
        }
    }

    private static VapourSynthPreviewLogLevel MapLogLevel(string? level)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            "warning" or "warn" => VapourSynthPreviewLogLevel.Warning,
            "error" => VapourSynthPreviewLogLevel.Error,
            _ => VapourSynthPreviewLogLevel.Information
        };
    }

    private VapourSynthPreviewLogLevel ClassifyHelperStderrLine(string line)
    {
        var normalizedLine = line.Trim();
        if (normalizedLine.Length == 0)
        {
            return VapourSynthPreviewLogLevel.Information;
        }

        if (normalizedLine.Contains("traceback", StringComparison.OrdinalIgnoreCase))
        {
            _stderrTracebackActive = true;
            return VapourSynthPreviewLogLevel.Error;
        }

        if (_stderrTracebackActive)
        {
            return VapourSynthPreviewLogLevel.Error;
        }

        if (ContainsAny(normalizedLine, "warning", "warn"))
        {
            return VapourSynthPreviewLogLevel.Warning;
        }

        if (ContainsAny(normalizedLine, "error", "failed", "exception", "fatal"))
        {
            return VapourSynthPreviewLogLevel.Error;
        }

        return VapourSynthPreviewLogLevel.Information;
    }

    private static bool ContainsAny(string line, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (line.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(VapourSynthPreviewService));
    }

    private sealed record StartupRequestDto(
        string? SourceFilePath,
        string DisplayName,
        string Content,
        string WorkingDirectory);

    private sealed record FrameCommandDto(
        string Command,
        int RequestId,
        int OutputIndex,
        int FrameNumber,
        string RawPath);

    private sealed record CloseCommandDto(string Command);

    private sealed class HostResponseDto
    {
        public string? Type { get; set; }

        public string? Level { get; set; }

        public string? Source { get; set; }

        public int RequestId { get; set; }

        public string? Message { get; set; }

        public string? RawPixelPath { get; set; }

        public int OutputIndex { get; set; }

        public int FrameNumber { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string? FrameType { get; set; }

        public List<HostOutputDto>? Outputs { get; set; }

        public List<HostPropertyDto>? Properties { get; set; }
    }

    private sealed class HostOutputDto
    {
        public int Index { get; set; }

        public string? Name { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int TotalFrames { get; set; }

        public int FpsNumerator { get; set; }

        public int FpsDenominator { get; set; }

        public string? FormatName { get; set; }

        public int BitsPerSample { get; set; }
    }

    private sealed class HostPropertyDto
    {
        public string? Key { get; set; }

        public string? Value { get; set; }
    }
}
