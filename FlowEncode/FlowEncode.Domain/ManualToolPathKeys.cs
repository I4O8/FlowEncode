namespace FlowEncode.Domain;

public static class ManualToolPathKeys
{
    public static string ForEncoder(EncoderKind kind) => $"encoder:{kind}";

    public static string ForRegisteredTool(RegisteredToolKind kind) => $"tool:{kind}";
}
