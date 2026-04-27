namespace FlowEncode.Domain;

public sealed record EncodingJobProgress(
    Guid JobId,
    EncodingJobState State,
    double? ProgressFraction,
    string Summary,
    string DetailLine,
    EncodingProgressSnapshot? Snapshot = null);
