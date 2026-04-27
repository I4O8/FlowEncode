namespace FlowEncode.Domain;

public sealed record EncodingProfile(
    EncoderKind Kind,
    string Name,
    string Description,
    string Preset,
    string Tune,
    string Profile,
    RateControlMode RateControl,
    double Quality,
    int? Bitrate,
    string OutputContainer,
    string AdditionalArguments,
    string UhdParameters)
{
    public string EncoderLabel => Kind.ToDisplayName();

    public string RateControlLabel => RateControl.ToDisplayLabel();

    public string QualitySummary =>
        RateControl switch
        {
            RateControlMode.Abr or RateControlMode.Vbr or RateControlMode.TwoPass => $"{Bitrate ?? 3500} kbps",
            _ => $"{RateControlLabel} {Quality:0.0##}"
        };
}
