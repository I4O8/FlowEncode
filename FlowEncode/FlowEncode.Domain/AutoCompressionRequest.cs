namespace FlowEncode.Domain;

public sealed record AutoCompressionRequest(
    Guid JobId,
    string SourcePath,
    string OutputPath,
    EncoderKind EncoderKind,
    double TargetVmaf,
    int Probes,
    string VideoParameters,
    int? Workers);
