namespace FlowEncode.Domain;

public enum ToolDetectionSource
{
    None,
    LocalToolset,
    LocalTools,
    EnvironmentVariable,
    Path,
    SystemEncoder,
    SpecialLocation
}
