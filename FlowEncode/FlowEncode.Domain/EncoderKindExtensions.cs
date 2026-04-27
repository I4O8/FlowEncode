namespace FlowEncode.Domain;

public static class EncoderKindExtensions
{
    public static string ToDisplayName(this EncoderKind kind) =>
        kind switch
        {
            EncoderKind.X264 => "x264 (AVC/H.264)",
            EncoderKind.X265 => "x265 (HEVC/H.265)",
            EncoderKind.SvtAv1 => "SVT-AV1",
            _ => kind.ToString()
        };

    public static string ToShortName(this EncoderKind kind) =>
        kind switch
        {
            EncoderKind.X264 => "x264",
            EncoderKind.X265 => "x265",
            EncoderKind.SvtAv1 => "svt-av1",
            _ => kind.ToString().ToLowerInvariant()
        };
}
