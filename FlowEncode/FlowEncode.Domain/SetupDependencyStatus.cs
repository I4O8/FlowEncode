namespace FlowEncode.Domain;

public sealed record SetupDependencyStatus(
    SetupDependencyKind Kind,
    ReadinessState State,
    string InstalledVersion,
    string LatestVersion,
    bool UpdateAvailable,
    string ExecutablePath,
    string ReleaseUrl,
    bool IsInstallSupported,
    bool IsInstallEnabled,
    string Detail);
