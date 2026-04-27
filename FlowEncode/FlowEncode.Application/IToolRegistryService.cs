using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IToolRegistryService
{
    IReadOnlyList<ToolDefinition> GetTools();

    ToolDefinition GetTool(RegisteredToolKind kind);

    IReadOnlyList<CapabilityDefinition> GetCapabilities();
}
