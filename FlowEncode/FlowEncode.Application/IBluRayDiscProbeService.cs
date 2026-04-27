using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IBluRayDiscProbeService
{
    Task<IReadOnlyList<BluRayPlaylistItem>> ScanDiscAsync(
        BluRayDemuxBackend backend,
        string discPath,
        CancellationToken cancellationToken = default);

    Task<BluRayPlaylistScanResult> ScanPlaylistAsync(
        BluRayDemuxBackend backend,
        string discPath,
        BluRayPlaylistItem playlist,
        CancellationToken cancellationToken = default);
}
