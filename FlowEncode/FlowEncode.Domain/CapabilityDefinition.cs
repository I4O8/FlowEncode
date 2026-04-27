namespace FlowEncode.Domain;

public sealed record CapabilityDefinition(
    EnvironmentCapabilityKind Kind,
    IReadOnlyList<CapabilityToolRequirement> Requirements);
