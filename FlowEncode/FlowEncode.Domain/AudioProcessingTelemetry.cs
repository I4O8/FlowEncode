namespace FlowEncode.Domain;

public sealed record AudioProcessingTelemetry(
    double? SpeedMultiplier,
    double? BitrateKbps,
    TimeSpan? Remaining,
    long? EstimatedOutputBytes);
