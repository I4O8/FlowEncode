namespace FlowEncode.Infrastructure;

internal sealed record SourceVideoInfo(
    int Width,
    int Height,
    long? TotalFrames,
    int BitDepth,
    int? FpsNumerator,
    int? FpsDenominator,
    string PixelFormat)
{
    public double? FramesPerSecond =>
        FpsNumerator is > 0 && FpsDenominator is > 0
            ? FpsNumerator.Value / (double)FpsDenominator.Value
            : null;
}
