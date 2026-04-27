namespace FlowEncode.Domain;

public sealed record CapabilityReadiness(
    EnvironmentCapabilityKind Kind,
    ReadinessState State,
    IReadOnlyList<CapabilityRequirementReadiness> Requirements)
{
    public int SatisfiedRequirementCount => Requirements.Count(static requirement => requirement.IsSatisfied);

    public int TotalRequirementCount => Requirements.Count;
}
