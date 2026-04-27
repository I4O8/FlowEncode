using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IAutoCompressionRunner
{
    Task<AutoCompressionResult> RunAsync(
        AutoCompressionRequest request,
        IProgress<AutoCompressionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string BuildDisplayCommand(AutoCompressionRequest request);

    void Abort(Guid jobId);
}
