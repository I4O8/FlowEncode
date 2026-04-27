namespace FlowEncode.Domain;

public sealed record ToolDefinition(
    RegisteredToolKind Kind,
    ToolProbeMode ProbeMode,
    IReadOnlyList<string> ExecutableNames,
    IReadOnlyList<string> EnvironmentVariableNames,
    ToolSearchLocation SearchLocations,
    string VersionArguments,
    string ReleaseUrl,
    ExternalToolKind? ManagedExternalToolKind = null,
    string ProbeValue = "")
{
    public string DisplayName => Kind.ToDisplayName();
}
