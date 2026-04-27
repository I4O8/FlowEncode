namespace FlowEncode.Domain;

public sealed record SetupDependencyStatusReport(
    DateTimeOffset CheckedAt,
    IReadOnlyList<SetupDependencyStatus> Dependencies);
