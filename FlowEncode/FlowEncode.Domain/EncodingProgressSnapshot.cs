namespace FlowEncode.Domain;

public sealed record EncodingProgressSnapshot(
    long? CurrentFrame,
    long? TotalFrames,
    double? FramesPerSecond,
    double? BitrateKbps,
    TimeSpan? Eta,
    long? EstimatedFileSizeBytes);
