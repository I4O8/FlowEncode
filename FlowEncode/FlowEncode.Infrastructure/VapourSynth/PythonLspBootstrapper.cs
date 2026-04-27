using System.Diagnostics;
using System.Text;

namespace FlowEncode.Infrastructure;

internal sealed class PythonLspBootstrapper : IDisposable
{
    private const string JediLanguageServerModule = "jedi_language_server";

    private readonly SemaphoreSlim _installLock = new(1, 1);

    public PythonLspBootstrapper(string runtimeRootPath)
    {
        if (string.IsNullOrWhiteSpace(runtimeRootPath))
        {
            throw new ArgumentException("Runtime root path cannot be empty.", nameof(runtimeRootPath));
        }

        RuntimeRootPath = runtimeRootPath;
        PackagesPath = Path.Combine(runtimeRootPath, "packages");
        Directory.CreateDirectory(RuntimeRootPath);
    }

    public string RuntimeRootPath { get; }

    public string PackagesPath { get; }

    public async Task<string?> EnsurePackagePathAsync(string pythonPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pythonPath))
        {
            throw new ArgumentException("Python path cannot be empty.", nameof(pythonPath));
        }

        if (await CanImportModuleAsync(pythonPath, JediLanguageServerModule, null, cancellationToken))
        {
            return null;
        }

        if (await CanImportModuleAsync(pythonPath, JediLanguageServerModule, PackagesPath, cancellationToken))
        {
            return PackagesPath;
        }

        await _installLock.WaitAsync(cancellationToken);

        try
        {
            if (await CanImportModuleAsync(pythonPath, JediLanguageServerModule, null, cancellationToken))
            {
                return null;
            }

            if (await CanImportModuleAsync(pythonPath, JediLanguageServerModule, PackagesPath, cancellationToken))
            {
                return PackagesPath;
            }

            Directory.CreateDirectory(PackagesPath);
            await EnsurePipAsync(pythonPath, cancellationToken);
            await InstallPackagesAsync(pythonPath, cancellationToken);

            if (!await CanImportModuleAsync(pythonPath, JediLanguageServerModule, PackagesPath, cancellationToken))
            {
                throw new InvalidOperationException("Python LSP bootstrap completed, but the jedi_language_server module is still unavailable.");
            }

            return PackagesPath;
        }
        finally
        {
            _installLock.Release();
        }
    }

    public void Dispose()
    {
        _installLock.Dispose();
    }

    private async Task EnsurePipAsync(string pythonPath, CancellationToken cancellationToken)
    {
        var pipVersion = await RunPythonCommandAsync(
            pythonPath,
            ["-m", "pip", "--version"],
            null,
            cancellationToken);

        if (pipVersion.ExitCode == 0)
        {
            return;
        }

        var ensurePip = await RunPythonCommandAsync(
            pythonPath,
            ["-m", "ensurepip", "--upgrade"],
            null,
            cancellationToken);

        if (ensurePip.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError(
                "Python pip is unavailable and ensurepip failed.",
                ensurePip));
        }
    }

    private async Task InstallPackagesAsync(string pythonPath, CancellationToken cancellationToken)
    {
        var install = await RunPythonCommandAsync(
            pythonPath,
            [
                "-m",
                "pip",
                "install",
                "--disable-pip-version-check",
                "--no-warn-script-location",
                "--upgrade",
                "--target",
                PackagesPath,
                "jedi-language-server",
                "jedi"
            ],
            null,
            cancellationToken);

        if (install.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError(
                "Failed to install Python LSP packages.",
                install));
        }
    }

    private static async Task<bool> CanImportModuleAsync(
        string pythonPath,
        string moduleName,
        string? additionalPythonPath,
        CancellationToken cancellationToken)
    {
        var result = await RunPythonCommandAsync(
            pythonPath,
            [
                "-c",
                $"import importlib.util, sys; sys.exit(0 if importlib.util.find_spec({QuotePythonString(moduleName)}) else 1)"
            ],
            additionalPythonPath,
            cancellationToken);

        return result.ExitCode == 0;
    }

    private static async Task<PythonCommandResult> RunPythonCommandAsync(
        string pythonPath,
        IReadOnlyList<string> arguments,
        string? additionalPythonPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        PythonProcessStartInfoHelper.ApplyUtf8(startInfo);
        ApplyAdditionalPythonPath(startInfo, additionalPythonPath);
        VapourSynthRuntimePathResolver.EnrichProcessPath(startInfo);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        using var _ = ErrorDialogSuppression.Enter();
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);

        return new PythonCommandResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static void ApplyAdditionalPythonPath(ProcessStartInfo startInfo, string? additionalPythonPath)
    {
        if (string.IsNullOrWhiteSpace(additionalPythonPath))
        {
            return;
        }

        var currentValue = startInfo.Environment.TryGetValue("PYTHONPATH", out var existing)
            ? existing ?? string.Empty
            : Environment.GetEnvironmentVariable("PYTHONPATH") ?? string.Empty;

        var segments = currentValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (!segments.Any(path => string.Equals(path, additionalPythonPath, StringComparison.OrdinalIgnoreCase)))
        {
            segments.Insert(0, additionalPythonPath);
        }

        startInfo.Environment["PYTHONPATH"] = string.Join(Path.PathSeparator, segments);
    }

    private static string BuildCommandError(string prefix, PythonCommandResult result)
    {
        var details = string.Join(
            Environment.NewLine,
            new[]
            {
                result.StandardError?.Trim(),
                result.StandardOutput?.Trim()
            }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(details)
            ? prefix
            : $"{prefix}{Environment.NewLine}{details}";
    }

    private static string QuotePythonString(string value)
    {
        return $"'{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)}'";
    }

    private sealed record PythonCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
