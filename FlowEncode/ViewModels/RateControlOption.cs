using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed record RateControlOption(
    RateControlMode Value,
    string Label);
