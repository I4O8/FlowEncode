namespace FlowEncode.Domain;

public sealed record EncodingJobRequest(
    Guid JobId,
    EncodingProfile Profile,
    string SourcePath,
    string OutputPath,
    InputPipelineKind PipelineKind,
    EncoderArchitecture PreferredArchitecture);
