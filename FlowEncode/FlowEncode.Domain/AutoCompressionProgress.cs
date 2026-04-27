namespace FlowEncode.Domain;

public sealed record AutoCompressionProgress(
    Guid JobId,
    EncodingJobState State,
    double? ProgressFraction,
    string Summary,
    string DetailLine);
