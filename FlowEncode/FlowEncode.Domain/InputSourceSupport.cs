namespace FlowEncode.Domain;

public static class InputSourceSupport
{
    public static IReadOnlyList<string> PreferredPickerExtensions { get; } =
    [
        ".avs",
        ".vpy",
        ".mkv",
        ".mp4",
        ".avi",
        ".flv",
        ".y4m",
        ".yuv"
    ];

    public static string PlaceholderExamples => ".avs / .vpy / .mkv / .mp4 / .avi / .flv / .y4m / .yuv";

    public static InputPipelineKind ResolvePipelineKind(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        return extension.ToLowerInvariant() switch
        {
            ".avs" => InputPipelineKind.AviSynth,
            ".vpy" => InputPipelineKind.VapourSynth,
            ".mkv" or ".mp4" or ".avi" or ".flv" => InputPipelineKind.FfmpegPipe,
            ".y4m" => InputPipelineKind.Y4mFile,
            ".yuv" => InputPipelineKind.RawYuvFile,
            _ => throw new NotSupportedException("当前仅支持 .avs、.vpy、.mkv、.mp4、.avi、.flv、.y4m、.yuv 作为输入。")
        };
    }
}
