using System.Text;
using System.Text.RegularExpressions;

namespace FlowEncode.Domain;

public static class ConsoleOutputLineNormalizer
{
    private static readonly Regex AnsiEscapeSequenceRegex = new(
        @"\x1B(?:\[[0-?]*[ -/]*[@-~]|\][^\a]*(?:\a|\x1B\\)|[@-Z\\-_])",
        RegexOptions.Compiled);

    public static string Normalize(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var sanitized = line.Trim().TrimStart('\uFEFF');
        if (sanitized.Length == 0)
        {
            return string.Empty;
        }

        sanitized = AnsiEscapeSequenceRegex.Replace(sanitized, string.Empty);
        if (sanitized.Length == 0)
        {
            return string.Empty;
        }

        StringBuilder? builder = null;
        for (var index = 0; index < sanitized.Length; index++)
        {
            var ch = sanitized[index];
            if (char.IsControl(ch) && ch != '\t')
            {
                builder ??= new StringBuilder(sanitized.Length).Append(sanitized, 0, index);
                continue;
            }

            builder?.Append(ch);
        }

        return (builder?.ToString() ?? sanitized).Trim();
    }
}
