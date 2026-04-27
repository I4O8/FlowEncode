namespace FlowEncode.Domain;

public sealed record DiscoveredEncoderBinary(
    EncoderKind Kind,
    EncoderArchitecture Architecture,
    string ExecutablePath,
    EncoderBinarySource Source,
    string SourceLabel,
    string DetectedVersion)
{
    public string KindLabel => Kind.ToDisplayName();

    public string ArchitectureLabel => Architecture == EncoderArchitecture.X64 ? "x64" : "x86";

    public string SourceTypeLabel => Source switch
    {
        EncoderBinarySource.EnvironmentVariable => "环境变量",
        EncoderBinarySource.Path => "PATH",
        EncoderBinarySource.LocalToolset => "本地工具链",
        _ => Source.ToString()
    };

    public string SourceSummary => $"{SourceTypeLabel} · {SourceLabel}";
}
