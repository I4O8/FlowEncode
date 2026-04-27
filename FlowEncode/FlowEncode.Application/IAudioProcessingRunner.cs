using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IAudioProcessingRunner
{
    Task<AudioProcessingResult> RunAsync(
        AudioProcessingRequest request,
        IProgress<AudioProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    string BuildDisplayCommand(AudioProcessingRequest request);

    void Abort(Guid jobId);
}
