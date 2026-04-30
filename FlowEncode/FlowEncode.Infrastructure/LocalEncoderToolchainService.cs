using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class LocalEncoderToolchainService : IEncoderToolchainService
{
    private readonly LocalAppPaths _paths;
    private readonly IEncoderDiscoveryService _discoveryService;
    private readonly IToolProbeService _toolProbeService;

    public LocalEncoderToolchainService(
        LocalAppPaths paths,
        IEncoderDiscoveryService discoveryService,
        IToolProbeService toolProbeService)
    {
        _paths = paths;
        _discoveryService = discoveryService;
        _toolProbeService = toolProbeService;
    }

    public Task<IReadOnlyList<EncoderCatalogItem>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<EncoderCatalogItem>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return EncoderManifestCatalog
                .GetAll()
                .Select(CreateCatalogItem)
                .ToList();
        }, cancellationToken);
    }

    public async Task ImportBinaryAsync(
        EncoderKind kind,
        EncoderArchitecture architecture,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected encoder binary was not found.", sourcePath);
        }

        var targetPath = _paths.GetBinaryPath(kind, architecture);
        var targetDirectory = Path.GetDirectoryName(targetPath) ?? _paths.ToolsetRootPath;
        Directory.CreateDirectory(targetDirectory);

        await using var sourceStream = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
        InvalidateProbeCaches();
    }

    public Task RemoveBinaryAsync(
        EncoderKind kind,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var architecture in Enum.GetValues<EncoderArchitecture>())
            {
                var path = _paths.GetBinaryPath(kind, architecture);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                TryDeleteDirectoryIfEmpty(Path.GetDirectoryName(path));
            }

            TryDeleteDirectoryIfEmpty(Path.Combine(_paths.ToolsetRootPath, kind.ToShortName()));
            InvalidateProbeCaches();
        }, cancellationToken);
    }

    public string GetToolsetRootPath() => _paths.ToolsetRootPath;

    private static void TryDeleteDirectoryIfEmpty(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(directory).Any())
        {
            return;
        }

        Directory.Delete(directory, false);
    }

    private void InvalidateProbeCaches()
    {
        _discoveryService.InvalidateCache();
        _toolProbeService.InvalidateCache();
    }

    private EncoderCatalogItem CreateCatalogItem(EncoderCapability capability)
    {
        var systemCandidates = _discoveryService
            .DiscoverSystemBinaries()
            .Where(candidate => candidate.Kind == capability.Kind)
            .ToList();

        var binaries = new[]
        {
            ProbeBinary(capability.Kind, EncoderArchitecture.X86),
            ProbeBinary(capability.Kind, EncoderArchitecture.X64)
        };

        var installedCount = binaries.Count(static binary => binary.Exists);
        var statusHeadline = systemCandidates.Count > 0
            ? $"已发现 {systemCandidates.Count} 个系统编码器，可直接调用"
            : installedCount switch
        {
            0 when capability.IsOptional => "可选编码器未安装",
            0 => "尚未导入本地编码器",
            1 => "已检测到 1 个本地二进制",
            _ => "x86 / x64 工具链均已就绪"
        };

        var statusDetails = systemCandidates.Count > 0
            ? $"设置页中检测到系统级 {capability.DisplayName}，可通过环境变量或 PATH 直接使用；仍然可以继续导入本地工具链。"
            : installedCount switch
        {
            0 when capability.IsOptional => "SVT-AV1 默认关闭。导入可执行文件后会自动参与后续预设和命令预览。",
            0 => "当前页面支持导入本地构建，并直接探测版本信息，便于后续做自动更新器。",
            _ => "版本信息来自已导入的可执行文件，刷新后会重新探测。"
        };

        return new EncoderCatalogItem(capability, binaries, statusHeadline, statusDetails);
    }

    private EncoderBinaryLocation ProbeBinary(EncoderKind kind, EncoderArchitecture architecture)
    {
        var path = _paths.GetBinaryPath(kind, architecture);
        var expectedFileName = LocalAppPaths.GetExpectedFileName(kind, architecture);
        var exists = File.Exists(path);
        var canExecute = architecture == EncoderArchitecture.X86 || Environment.Is64BitOperatingSystem;

        string detectedVersion;
        string statusLabel;

        if (!exists)
        {
            detectedVersion = "Missing";
            statusLabel = "等待导入";
        }
        else if (!canExecute)
        {
            detectedVersion = "Present (execution unavailable)";
            statusLabel = "当前系统无法执行";
        }
        else
        {
            detectedVersion = EncoderBinaryProbe.ProbeVersion(path, kind);
            statusLabel = "版本已探测";
        }

        return new EncoderBinaryLocation(
            kind,
            architecture,
            path,
            expectedFileName,
            exists,
            canExecute,
            detectedVersion,
            statusLabel);
    }
}
