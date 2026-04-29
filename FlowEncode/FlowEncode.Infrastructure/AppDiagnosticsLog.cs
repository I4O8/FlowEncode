using System.Globalization;
using System.Text;

namespace FlowEncode.Infrastructure;

internal static class AppDiagnosticsLog
{
    private static readonly object SyncRoot = new();
    private const string FileName = "diagnostics.log";

    public static void Write(LocalAppPaths appPaths, string source, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(appPaths.LogsRootPath);
            var path = Path.Combine(appPaths.LogsRootPath, FileName);
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {source}: {message}{Environment.NewLine}");

            lock (SyncRoot)
            {
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
        }
        catch
        {
        }
    }
}
