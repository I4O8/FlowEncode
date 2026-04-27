namespace FlowEncode.Domain;

public static class ReadinessStateResolver
{
    public static ReadinessState ResolveFromRequirements(IReadOnlyList<CapabilityRequirementReadiness> requirements)
    {
        if (requirements.Count == 0)
        {
            return ReadinessState.Unknown;
        }

        if (requirements.All(static requirement => requirement.IsSatisfied))
        {
            return ReadinessState.Ready;
        }

        if (requirements.Any(static requirement =>
                !requirement.IsSatisfied
                && requirement.CandidateResults.Any(static candidate => candidate.State == ReadinessState.Misconfigured)))
        {
            return ReadinessState.Misconfigured;
        }

        if (requirements.Any(static requirement => requirement.IsSatisfied))
        {
            return ReadinessState.Partial;
        }

        var allUnknown = requirements.All(static requirement =>
            requirement.CandidateResults.All(static candidate => candidate.State == ReadinessState.Unknown));

        return allUnknown
            ? ReadinessState.Unknown
            : ReadinessState.Missing;
    }
}
