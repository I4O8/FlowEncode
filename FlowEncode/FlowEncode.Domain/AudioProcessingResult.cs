namespace FlowEncode.Domain;

public sealed record AudioProcessingResult(
    Guid JobId,
    EncodingJobState State,
    int ExitCode,
    string Summary,
    string Log,
    string DisplayCommand);
