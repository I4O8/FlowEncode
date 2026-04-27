namespace FlowEncode.Domain;

public sealed record BluRayDemuxResult(
    Guid JobId,
    EncodingJobState State,
    int ExitCode,
    string Summary,
    string Log,
    string DisplayCommand,
    IReadOnlyList<string> OutputPaths);
