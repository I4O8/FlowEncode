namespace FlowEncode.Domain;

public sealed record EncoderCatalogItem(
    EncoderCapability Capability,
    IReadOnlyList<EncoderBinaryLocation> Binaries,
    string StatusHeadline,
    string StatusDetails)
{
    public bool HasAnyBinary => Binaries.Any(static binary => binary.Exists);

    public int ReadyBinaryCount => Binaries.Count(static binary => binary.Exists);
}
