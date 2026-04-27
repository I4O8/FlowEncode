namespace FlowEncode.Domain;

public sealed record DiscoveredExternalToolBinary(
    ExternalToolKind Kind,
    string ExecutablePath,
    ExternalToolBinarySource Source,
    string SourceLabel,
    string DetectedVersion)
{
    public string KindLabel => Kind.ToDisplayName();

    public string SourceTypeLabel => Source switch
    {
        ExternalToolBinarySource.LocalTools => "本地工具目录",
        ExternalToolBinarySource.Path => "PATH",
        _ => Source.ToString()
    };

    public string SourceSummary => $"{SourceTypeLabel} · {SourceLabel}";
}

