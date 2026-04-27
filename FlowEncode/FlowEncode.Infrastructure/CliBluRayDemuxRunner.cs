using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class CliBluRayDemuxRunner : IBluRayDemuxRunner
{
    private readonly IReadOnlyDictionary<BluRayDemuxBackend, IBluRayDemuxBackendAdapter> _adapters;

    public CliBluRayDemuxRunner(IEnumerable<IBluRayDemuxBackendAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(static adapter => adapter.Backend);
    }

    public Task<BluRayDemuxResult> RunAsync(
        BluRayDemuxRequest request,
        IProgress<BluRayDemuxProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return ResolveAdapter(request.Backend).RunAsync(request, progress, cancellationToken);
    }

    public string BuildDisplayCommand(BluRayDemuxRequest request)
    {
        return ResolveAdapter(request.Backend).BuildDisplayCommand(request);
    }

    public void Abort(Guid jobId, BluRayDemuxBackend backend)
    {
        ResolveAdapter(backend).Abort(jobId);
    }

    private IBluRayDemuxBackendAdapter ResolveAdapter(BluRayDemuxBackend backend)
    {
        return _adapters.TryGetValue(backend, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"Blu-ray backend adapter not registered: {backend}");
    }
}
