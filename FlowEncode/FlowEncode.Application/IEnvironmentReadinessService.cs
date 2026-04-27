using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IEnvironmentReadinessService
{
    Task<EnvironmentReadinessReport> CheckAsync(CancellationToken cancellationToken = default);
}
