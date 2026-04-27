using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class CliBluRayDiscProbeService : IBluRayDiscProbeService
{
    private readonly IReadOnlyDictionary<BluRayDemuxBackend, IBluRayDemuxBackendAdapter> _adapters;

    public CliBluRayDiscProbeService(IEnumerable<IBluRayDemuxBackendAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(static adapter => adapter.Backend);
    }

    public Task<IReadOnlyList<BluRayPlaylistItem>> ScanDiscAsync(
        BluRayDemuxBackend backend,
        string discPath,
        CancellationToken cancellationToken = default)
    {
        return ResolveAdapter(backend).ScanDiscAsync(discPath, cancellationToken);
    }

    public Task<BluRayPlaylistScanResult> ScanPlaylistAsync(
        BluRayDemuxBackend backend,
        string discPath,
        BluRayPlaylistItem playlist,
        CancellationToken cancellationToken = default)
    {
        return ResolveAdapter(backend).ScanPlaylistAsync(discPath, playlist, cancellationToken);
    }

    private IBluRayDemuxBackendAdapter ResolveAdapter(BluRayDemuxBackend backend)
    {
        return _adapters.TryGetValue(backend, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"Blu-ray backend adapter not registered: {backend}");
    }
}
