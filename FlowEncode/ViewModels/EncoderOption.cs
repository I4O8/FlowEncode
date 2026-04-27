using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed record EncoderOption(
    EncoderKind Value,
    string Label);
