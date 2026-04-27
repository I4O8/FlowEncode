namespace FlowEncode.Domain;

public sealed record SetupInstallProgress(
    SetupDependencyKind Kind,
    double Percent,
    string StatusText,
    bool IsIndeterminate = false);
