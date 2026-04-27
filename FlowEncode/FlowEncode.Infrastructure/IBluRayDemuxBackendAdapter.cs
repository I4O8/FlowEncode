using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public interface IBluRayDemuxBackendAdapter
{
    BluRayDemuxBackend Backend { get; }

    Task<IReadOnlyList<BluRayPlaylistItem>> ScanDiscAsync(
        string discPath,
        CancellationToken cancellationToken = default);

    Task<BluRayPlaylistScanResult> ScanPlaylistAsync(
        string discPath,
        BluRayPlaylistItem playlist,
        CancellationToken cancellationToken = default);

    Task<BluRayDemuxResult> RunAsync(
        BluRayDemuxRequest request,
        IProgress<BluRayDemuxProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string BuildDisplayCommand(BluRayDemuxRequest request);

    void Abort(Guid jobId);
}
