namespace FlowEncode.Domain;

public sealed record CommandPreview(
    string Title,
    string CommandLine,
    string Notes);
