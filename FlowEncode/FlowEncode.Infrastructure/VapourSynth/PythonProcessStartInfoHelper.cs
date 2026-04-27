using System.Diagnostics;

namespace FlowEncode.Infrastructure;

internal static class PythonProcessStartInfoHelper
{
    public static void ApplyUtf8(ProcessStartInfo startInfo)
    {
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
    }
}
