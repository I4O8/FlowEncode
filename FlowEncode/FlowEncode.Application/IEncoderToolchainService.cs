using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IEncoderToolchainService
{
    Task<IReadOnlyList<EncoderCatalogItem>> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task ImportBinaryAsync(
        EncoderKind kind,
        EncoderArchitecture architecture,
        string sourcePath,
        CancellationToken cancellationToken = default);

    Task RemoveBinaryAsync(
        EncoderKind kind,
        CancellationToken cancellationToken = default);

    string GetToolsetRootPath();
}
