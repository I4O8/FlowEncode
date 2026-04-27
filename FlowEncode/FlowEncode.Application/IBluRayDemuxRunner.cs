using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IBluRayDemuxRunner
{
    Task<BluRayDemuxResult> RunAsync(
        BluRayDemuxRequest request,
        IProgress<BluRayDemuxProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string BuildDisplayCommand(BluRayDemuxRequest request);

    void Abort(Guid jobId, BluRayDemuxBackend backend);
}
