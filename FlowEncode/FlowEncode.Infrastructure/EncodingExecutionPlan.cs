using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

internal sealed record EncodingExecutionStep(
    string FileName,
    string Arguments,
    string DisplayCommand,
    int StageIndex,
    int StageCount);

internal sealed record EncodingExecutionPlan(
    IReadOnlyList<EncodingExecutionStep> Steps,
    string DisplayCommand,
    EncoderKind Kind,
    long? TotalFrames,
    double? SourceFramesPerSecond,
    IReadOnlyList<string>? CleanupPaths = null);
