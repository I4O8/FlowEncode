namespace FlowEncode.Domain;

public sealed record EncoderBinaryLocation(
    EncoderKind Kind,
    EncoderArchitecture Architecture,
    string LocalPath,
    string ExpectedFileName,
    bool Exists,
    bool CanExecute,
    string DetectedVersion,
    string StatusLabel)
{
    public string ArchitectureLabel => Architecture == EncoderArchitecture.X64 ? "x64" : "x86";

    public string ImportToken => $"{Kind}|{Architecture}";
}
