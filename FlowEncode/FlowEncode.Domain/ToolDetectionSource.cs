namespace FlowEncode.Domain;

public enum ToolDetectionSource
{
    None,
    ManualSelection,
    LocalToolset,
    LocalTools,
    EnvironmentVariable,
    Path,
    SystemEncoder,
    SpecialLocation
}
