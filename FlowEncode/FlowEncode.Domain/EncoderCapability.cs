namespace FlowEncode.Domain;

public sealed record EncoderCapability(
    EncoderKind Kind,
    string Description,
    IReadOnlyList<string> Presets,
    IReadOnlyList<string> Tunes,
    IReadOnlyList<string> Profiles,
    IReadOnlyList<RateControlMode> RateControlModes,
    IReadOnlyList<string> OutputFormats,
    bool IsOptional,
    IReadOnlyList<EncoderUpdateChannel> UpdateChannels)
{
    public string DisplayName => Kind.ToDisplayName();

    public string SupportBadge => IsOptional ? "Optional" : "Core";

    public string PresetSummary => string.Join(", ", Presets.Take(6)) + (Presets.Count > 6 ? " ..." : string.Empty);

    public string TuneSummary => Tunes.Count == 0 ? "None" : string.Join(", ", Tunes.Take(5)) + (Tunes.Count > 5 ? " ..." : string.Empty);

    public string ProfileSummary => Profiles.Count == 0 ? "Auto" : string.Join(", ", Profiles.Take(5)) + (Profiles.Count > 5 ? " ..." : string.Empty);
}
