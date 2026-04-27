using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed record AudioWorkflowOption(
    AudioProcessingMode Value,
    string Label);
