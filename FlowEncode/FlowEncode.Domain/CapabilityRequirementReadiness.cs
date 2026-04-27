namespace FlowEncode.Domain;

public sealed record CapabilityRequirementReadiness(
    CapabilityToolRequirement Requirement,
    IReadOnlyList<ToolProbeResult> CandidateResults)
{
    public bool IsSatisfied => CandidateResults.Any(static result => result.State == ReadinessState.Ready);
}
