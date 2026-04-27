using System;
using System.IO;

namespace FlowEncode;

public sealed class AppLaunchActivation
{
    public string? RequestedVapourSynthFilePath { get; private set; }

    public bool HasRequestedVapourSynthFile
        => !string.IsNullOrWhiteSpace(RequestedVapourSynthFilePath);

    public void SetRequestedVapourSynthFilePath(string? filePath)
    {
        RequestedVapourSynthFilePath = NormalizeSupportedScriptPath(filePath);
    }

    public static string? NormalizeSupportedScriptPath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (!IsSupportedScriptExtension(filePath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsSupportedScriptExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".vpy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase);
    }
}
