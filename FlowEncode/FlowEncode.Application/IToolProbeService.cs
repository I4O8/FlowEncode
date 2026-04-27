using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IToolProbeService
{
    Task<IReadOnlyList<ToolProbeResult>> ProbeAllAsync(CancellationToken cancellationToken = default);

    Task<ToolProbeResult> ProbeAsync(RegisteredToolKind kind, CancellationToken cancellationToken = default);
}
