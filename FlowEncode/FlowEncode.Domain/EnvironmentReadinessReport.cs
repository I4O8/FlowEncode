namespace FlowEncode.Domain;

public sealed record EnvironmentReadinessReport(
    DateTimeOffset CheckedAt,
    IReadOnlyList<ToolProbeResult> Tools,
    IReadOnlyList<CapabilityReadiness> Capabilities)
{
    public int ReadyCapabilityCount => Capabilities.Count(static capability => capability.State == ReadinessState.Ready);
}
