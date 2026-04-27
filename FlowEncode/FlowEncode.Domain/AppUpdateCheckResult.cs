namespace FlowEncode.Domain;

public sealed record AppUpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    DateTimeOffset PublishedAt,
    bool HasPublishedRelease,
    bool VersionsComparable,
    bool UpdateAvailable,
    bool IsCurrentVersionNewerThanRelease,
    string? InstallerAssetName,
    string? InstallerDownloadUrl,
    string? InstallerSha256)
{
    public bool HasInstallerAsset =>
        !string.IsNullOrWhiteSpace(InstallerAssetName)
        && !string.IsNullOrWhiteSpace(InstallerDownloadUrl);

    public bool CanDownloadInstaller =>
        HasInstallerAsset
        && !string.IsNullOrWhiteSpace(InstallerSha256);
}
