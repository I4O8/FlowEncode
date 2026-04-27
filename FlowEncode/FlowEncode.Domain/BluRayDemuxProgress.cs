namespace FlowEncode.Domain;

public sealed record BluRayDemuxProgress(
    Guid JobId,
    EncodingJobState State,
    double? ProgressFraction,
    string Summary,
    string DetailLine);
