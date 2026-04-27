using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed record ThemeOption(
    AppThemePreference Value,
    string Label);
