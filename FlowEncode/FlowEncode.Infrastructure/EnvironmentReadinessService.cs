using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class EnvironmentReadinessService : IEnvironmentReadinessService
{
    private readonly IToolRegistryService _toolRegistryService;
    private readonly IToolProbeService _toolProbeService;

    public EnvironmentReadinessService(
        IToolRegistryService toolRegistryService,
        IToolProbeService toolProbeService)
    {
        _toolRegistryService = toolRegistryService;
        _toolProbeService = toolProbeService;
    }

    public async Task<EnvironmentReadinessReport> CheckAsync(CancellationToken cancellationToken = default)
    {
        var toolResults = await _toolProbeService.ProbeAllAsync(cancellationToken);
        var toolMap = toolResults.ToDictionary(static result => result.Kind, static result => result);

        var capabilityResults = _toolRegistryService
            .GetCapabilities()
            .Select(definition => BuildCapabilityReadiness(definition, toolMap))
            .ToList();

        return new EnvironmentReadinessReport(DateTimeOffset.Now, toolResults, capabilityResults);
    }

    private static CapabilityReadiness BuildCapabilityReadiness(
        CapabilityDefinition definition,
        IReadOnlyDictionary<RegisteredToolKind, ToolProbeResult> toolMap)
    {
        var requirements = definition.Requirements
            .Select(requirement => new CapabilityRequirementReadiness(
                requirement,
                requirement.CandidateTools.Select(toolKind => toolMap[toolKind]).ToList()))
            .ToList();

        return new CapabilityReadiness(
            definition.Kind,
            ReadinessStateResolver.ResolveFromRequirements(requirements),
            requirements);
    }
}
