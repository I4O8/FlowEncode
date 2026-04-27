using FlowEncode.Domain;
using System.Text.Json;

namespace FlowEncode.Infrastructure;

public sealed class LocalAppPaths
{
    private const string AppFolderName = "FlowEncode";
    private const string WorkspaceRootPathPropertyName = "workspaceRootPath";

    public LocalAppPaths()
    {
        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        LocalStateRootPath = Path.Combine(
            localApplicationDataPath,
            AppFolderName);
        InstallRootPath = ResolveExecutableDirectory(LocalStateRootPath);
        DataRootPath = Path.Combine(LocalStateRootPath, "data");
        SettingsRootPath = Path.Combine(DataRootPath, "settings");
        LocalizationRootPath = Path.Combine(DataRootPath, "localization");
        LogsRootPath = Path.Combine(DataRootPath, "logs");
        SettingsPath = Path.Combine(SettingsRootPath, "settings.json");
        SetupGuideCachePath = Path.Combine(SettingsRootPath, "setup-guide-cache.json");

        RootPath = ResolveStartupWorkspaceRootPath(
            ReadConfiguredWorkspaceRootPath(localApplicationDataPath),
            localApplicationDataPath,
            InstallRootPath);
        WorkspaceRootPath = RootPath;
        DownloadsRootPath = Path.Combine(RootPath, "downloads");
        ToolDataRootPath = Path.Combine(RootPath, "encoders");
        ToolsetRootPath = ToolDataRootPath;
        ToolsRootPath = Path.Combine(RootPath, "tools");
        WorkspaceTemplatesRootPath = Path.Combine(RootPath, "Templates");

        Directory.CreateDirectory(DataRootPath);
        Directory.CreateDirectory(SettingsRootPath);
        Directory.CreateDirectory(LocalizationRootPath);
        Directory.CreateDirectory(LogsRootPath);
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(DownloadsRootPath);
        Directory.CreateDirectory(ToolsetRootPath);
        Directory.CreateDirectory(ToolsRootPath);
        Directory.CreateDirectory(WorkspaceTemplatesRootPath);
    }

    public string LocalStateRootPath { get; }

    public string InstallRootPath { get; }

    public string RootPath { get; }

    public string WorkspaceRootPath { get; }

    public string DataRootPath { get; }

    public string SettingsRootPath { get; }

    public string LocalizationRootPath { get; }

    public string LogsRootPath { get; }

    public string ToolDataRootPath { get; }

    public string ToolsetRootPath { get; }

    public string DownloadsRootPath { get; }

    public string ToolsRootPath { get; }

    public string WorkspaceTemplatesRootPath { get; }

    public string SettingsPath { get; }

    public string SetupGuideCachePath { get; }

    public string NormalizeWorkspaceRootPath(string? configuredWorkspaceRootPath)
    {
        return ResolveWorkspaceRootPath(configuredWorkspaceRootPath, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    public bool IsWorkspaceRootInsideInstallRoot(string workspaceRootPath)
    {
        return IsWorkspaceRootInsideInstallRoot(workspaceRootPath, InstallRootPath);
    }

    public bool IsWorkspaceRootInsideProgramFiles(string workspaceRootPath)
    {
        return IsWorkspaceRootInsideProgramFilesCore(workspaceRootPath);
    }

    private static bool IsWorkspaceRootInsideInstallRoot(string workspaceRootPath, string installRootPath)
    {
        return IsSameOrChildPath(workspaceRootPath, installRootPath)
            || IsSameOrChildPath(installRootPath, workspaceRootPath);
    }

    private static bool IsWorkspaceRootInsideProgramFilesCore(string workspaceRootPath)
    {
        var programFilesPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        return programFilesPaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Any(programFilesPath => IsSameOrChildPath(workspaceRootPath, programFilesPath));
    }

    public void PrepareWorkspaceRootChange(string configuredWorkspaceRootPath)
    {
        var targetWorkspaceRootPath = NormalizeWorkspaceRootPath(configuredWorkspaceRootPath);
        if (AreSamePath(targetWorkspaceRootPath, RootPath))
        {
            return;
        }

        Directory.CreateDirectory(targetWorkspaceRootPath);
        CopyDirectoryContentsIfMissing(DownloadsRootPath, Path.Combine(targetWorkspaceRootPath, "downloads"));
        CopyDirectoryContentsIfMissing(ToolDataRootPath, Path.Combine(targetWorkspaceRootPath, "encoders"));
        CopyDirectoryContentsIfMissing(ToolsRootPath, Path.Combine(targetWorkspaceRootPath, "tools"));
        CopyDirectoryContentsIfMissing(WorkspaceTemplatesRootPath, Path.Combine(targetWorkspaceRootPath, "Templates"));
    }

    private static string ResolveExecutableDirectory(string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            var processDirectory = Path.GetDirectoryName(Environment.ProcessPath);
            if (!string.IsNullOrWhiteSpace(processDirectory))
            {
                return processDirectory;
            }
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            return AppContext.BaseDirectory;
        }

        return fallbackDirectory;
    }

    private static string ResolveStartupWorkspaceRootPath(
        string? configuredWorkspaceRootPath,
        string localApplicationDataPath,
        string installRootPath)
    {
        foreach (var candidatePath in EnumerateWorkspaceRootCandidates(configuredWorkspaceRootPath, localApplicationDataPath))
        {
            if (string.IsNullOrWhiteSpace(candidatePath)
                || IsWorkspaceRootInsideInstallRoot(candidatePath, installRootPath)
                || IsWorkspaceRootInsideProgramFilesCore(candidatePath))
            {
                continue;
            }

            if (TryEnsureWorkspaceRootAvailable(candidatePath, out var resolvedWorkspaceRootPath))
            {
                return resolvedWorkspaceRootPath;
            }
        }

        return Path.Combine(localApplicationDataPath, AppFolderName, "workspace");
    }

    private static IEnumerable<string> EnumerateWorkspaceRootCandidates(string? configuredWorkspaceRootPath, string localApplicationDataPath)
    {
        var normalizedConfiguredPath = NormalizeExplicitWorkspaceRootPath(configuredWorkspaceRootPath);
        if (!string.IsNullOrWhiteSpace(normalizedConfiguredPath))
        {
            yield return normalizedConfiguredPath;
        }

        var preferredDriveRoot = ResolvePreferredNonSystemDriveRootPath();
        if (!string.IsNullOrWhiteSpace(preferredDriveRoot))
        {
            yield return Path.Combine(preferredDriveRoot, AppFolderName);
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documentsPath))
        {
            yield return Path.Combine(documentsPath, AppFolderName);
        }

        yield return Path.Combine(localApplicationDataPath, AppFolderName, "workspace");
    }

    private static string ResolveWorkspaceRootPath(string? configuredWorkspaceRootPath, string localApplicationDataPath)
    {
        var normalizedConfiguredPath = NormalizeExplicitWorkspaceRootPath(configuredWorkspaceRootPath);
        if (!string.IsNullOrWhiteSpace(normalizedConfiguredPath))
        {
            return normalizedConfiguredPath;
        }

        var preferredDriveRoot = ResolvePreferredNonSystemDriveRootPath();
        if (!string.IsNullOrWhiteSpace(preferredDriveRoot))
        {
            return Path.Combine(preferredDriveRoot, AppFolderName);
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documentsPath))
        {
            return Path.Combine(documentsPath, AppFolderName);
        }

        return Path.Combine(localApplicationDataPath, AppFolderName, "workspace");
    }

    private static string? NormalizeExplicitWorkspaceRootPath(string? configuredWorkspaceRootPath)
    {
        if (string.IsNullOrWhiteSpace(configuredWorkspaceRootPath))
        {
            return null;
        }

        var trimmed = configuredWorkspaceRootPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(trimmed);
        return Path.GetFullPath(expanded);
    }

    private static string? ResolvePreferredNonSystemDriveRootPath()
    {
        try
        {
            var systemDriveRoot = Path.GetPathRoot(Environment.SystemDirectory);
            return DriveInfo.GetDrives()
                .Where(static drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
                .Where(drive => !string.Equals(drive.RootDirectory.FullName, systemDriveRoot, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static drive => drive.AvailableFreeSpace)
                .ThenBy(static drive => drive.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static drive => drive.RootDirectory.FullName)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadConfiguredWorkspaceRootPath(string localApplicationDataPath)
    {
        var settingsPath = Path.Combine(localApplicationDataPath, AppFolderName, "data", "settings", "settings.json");
        if (!File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            return TryGetStringProperty(document.RootElement, WorkspaceRootPathPropertyName, out var value)
                && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.ToString()
            };
            return true;
        }

        return false;
    }

    private static void CopyDirectoryContentsIfMissing(string sourceRootPath, string targetRootPath)
    {
        if (!Directory.Exists(sourceRootPath) || AreSamePath(sourceRootPath, targetRootPath))
        {
            return;
        }

        foreach (var sourceDirectory in Directory.EnumerateDirectories(sourceRootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRootPath, sourceDirectory);
            Directory.CreateDirectory(Path.Combine(targetRootPath, relativePath));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRootPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRootPath, sourceFile);
            var targetFilePath = Path.Combine(targetRootPath, relativePath);
            if (File.Exists(targetFilePath))
            {
                continue;
            }

            var targetDirectory = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourceFile, targetFilePath, false);
        }
    }

    private static bool TryEnsureWorkspaceRootAvailable(string workspaceRootPath, out string resolvedWorkspaceRootPath)
    {
        resolvedWorkspaceRootPath = string.Empty;

        try
        {
            var normalizedWorkspaceRootPath = Path.GetFullPath(workspaceRootPath);
            Directory.CreateDirectory(normalizedWorkspaceRootPath);

            var probeFilePath = Path.Combine(normalizedWorkspaceRootPath, $".flowencode-probe-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probeFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            resolvedWorkspaceRootPath = normalizedWorkspaceRootPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool AreSamePath(string leftPath, string rightPath)
    {
        try
        {
            var left = Path.GetFullPath(leftPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var right = Path.GetFullPath(rightPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSameOrChildPath(string path, string basePath)
    {
        try
        {
            var normalizedBasePath = Path.GetFullPath(basePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedPath, normalizedBasePath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return (normalizedPath + Path.DirectorySeparatorChar)
                .StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public string GetBinaryDirectory(EncoderKind kind, EncoderArchitecture architecture)
    {
        var encoderFolder = kind.ToShortName();
        var archFolder = architecture == EncoderArchitecture.X64 ? "x64" : "x86";

        return Path.Combine(ToolsetRootPath, encoderFolder, archFolder);
    }

    public string GetBinaryPath(EncoderKind kind, EncoderArchitecture architecture)
    {
        return Path.Combine(GetBinaryDirectory(kind, architecture), GetExpectedFileName(kind, architecture));
    }

    public static string GetExpectedFileName(EncoderKind kind, EncoderArchitecture architecture)
    {
        var arch = architecture == EncoderArchitecture.X64 ? "x64" : "x86";

        return kind switch
        {
            EncoderKind.X264 => $"x264_{arch}.exe",
            EncoderKind.X265 => $"x265_{arch}.exe",
            EncoderKind.SvtAv1 => $"SvtAv1EncApp_{arch}.exe",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
