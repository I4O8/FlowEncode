using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed record AudioEac3ToOutputFormatOption(
    AudioEac3ToOutputFormat Value,
    string Label);
