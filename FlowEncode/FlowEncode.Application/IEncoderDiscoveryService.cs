using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IEncoderDiscoveryService
{
    IReadOnlyList<DiscoveredEncoderBinary> DiscoverSystemBinaries();

    DiscoveredEncoderBinary? ResolveEncoder(
        EncoderKind kind,
        EncoderArchitecture preferredArchitecture,
        bool preferSystemEncoders);

    void InvalidateCache();
}
