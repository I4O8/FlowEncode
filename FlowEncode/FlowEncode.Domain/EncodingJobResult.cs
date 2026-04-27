namespace FlowEncode.Domain;

public sealed record EncodingJobResult(
    Guid JobId,
    EncodingJobState State,
    int ExitCode,
    string Summary,
    string Log,
    string LogFilePath);
