namespace FlowEncode.Domain;

public static class ExternalToolKindExtensions
{
    public static string ToDisplayName(this ExternalToolKind kind)
    {
        return kind switch
        {
            ExternalToolKind.Av1an => "Av1an",
            ExternalToolKind.Ffmpeg => "FFmpeg",
            _ => kind.ToString()
        };
    }

    public static string ToExpectedExecutableName(this ExternalToolKind kind)
    {
        return kind switch
        {
            ExternalToolKind.Av1an => "av1an.exe",
            ExternalToolKind.Ffmpeg => "ffmpeg.exe",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}

