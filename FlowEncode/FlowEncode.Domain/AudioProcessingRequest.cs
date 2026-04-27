namespace FlowEncode.Domain;

public sealed record AudioProcessingRequest(
    Guid JobId,
    string SourcePath,
    string OutputPath,
    AudioProcessingMode Mode,
    AudioEac3ToOutputFormat? Eac3ToOutputFormat,
    IReadOnlyList<string> Eac3ToAdditionalArguments,
    double? SourceDurationSeconds,
    int? OpusBitrateKbps,
    bool UseOpusMappingFamily1);
