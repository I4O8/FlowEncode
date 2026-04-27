namespace FlowEncode.Domain;

public sealed record CapabilityToolRequirement(IReadOnlyList<RegisteredToolKind> CandidateTools)
{
    public CapabilityToolRequirement(params RegisteredToolKind[] candidateTools)
        : this(Array.AsReadOnly(candidateTools))
    {
    }
}
