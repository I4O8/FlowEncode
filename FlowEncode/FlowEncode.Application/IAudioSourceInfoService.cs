using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IAudioSourceInfoService
{
    Task<AudioSourceInfo?> ProbeAsync(string sourcePath, CancellationToken cancellationToken = default);
}
