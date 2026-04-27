using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IEncodingJobRunner
{
    Task<EncodingJobResult> RunAsync(
        EncodingJobRequest request,
        IProgress<EncodingJobProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string BuildDisplayCommand(EncodingJobRequest request);

    void AbortJob(Guid jobId);
}
