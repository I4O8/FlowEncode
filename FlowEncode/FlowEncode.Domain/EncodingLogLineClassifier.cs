namespace FlowEncode.Domain;

public static class EncodingLogLineClassifier
{
    private static readonly System.Text.RegularExpressions.Regex X26xPrefixedProgressRegex = new(
        @"^(?:x26[45]\s+)?\[?\s*\d{1,3}(?:\.\d+)?\s*%\]?\s+.*\b(?:frame|frames)\b.*\bfps\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex X26xBracketedProgressRegex = new(
        @"^\[?\s*\d{1,3}(?:\.\d+)?\s*%\]?\s+(?:\d+\s*\/\s*\d+\s+frames|\d+\s+frames:)",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex X26xFrameTickerRegex = new(
        @"^\d+\s+frames:\s*\d+(?:\.\d+)?\s+fps,\s*\d+(?:\.\d+)?\s+kb\/s(?:,\s*eta\s+\d+:\d{2}:\d{2})?(?:,\s*est\.\s*file\s*size\s+\d+(?:\.\d+)?\s*[KMGTP]?B)?$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex X26xFfmpegStyleTickerRegex = new(
        @"\bframe=\s*\d+\b.*\bfps=\s*\d+(?:\.\d+)?\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex X26xPipeTickerRegex = new(
        @"^(?:x26[45]\s+)?\d+\s+frames\s+@\s+\d+(?:\.\d+)?\s+fps\s*\|\s*\d+(?:\.\d+)?\s+kb\/s\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex SvtLegacyFrameRegex = new(
        @"^Encoding\s+frame\s+\d+\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex SvtStatusTickerRegex = new(
        @"^Encoding:\s*\d+\s*\/\s*\d+\s+Frames?\b.*\bfps\b.*\bkbps\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static readonly System.Text.RegularExpressions.Regex SvtOutputFrameRegex = new(
        @"^Output\s+\d+\s+frames\b",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static bool IsTransientProgressLine(EncoderKind kind, string? line)
    {
        var normalized = line?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return kind switch
        {
            EncoderKind.X264 or EncoderKind.X265 => LooksLikeX26xProgress(normalized),
            EncoderKind.SvtAv1 => LooksLikeSvtProgress(normalized),
            _ => false
        };
    }

    private static bool LooksLikeX26xProgress(string line)
    {
        var normalized = NormalizeX26xProgressPrefix(line);
        if (normalized.StartsWith("encoded ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return X26xBracketedProgressRegex.IsMatch(normalized)
            || X26xFrameTickerRegex.IsMatch(normalized)
            || X26xPrefixedProgressRegex.IsMatch(normalized)
            || X26xFfmpegStyleTickerRegex.IsMatch(normalized)
            || X26xPipeTickerRegex.IsMatch(normalized);
    }

    private static bool LooksLikeSvtProgress(string line)
    {
        if (string.Equals(line, "Encoding", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SvtLegacyFrameRegex.IsMatch(line)
            || SvtStatusTickerRegex.IsMatch(line)
            || SvtOutputFrameRegex.IsMatch(line);
    }

    private static string NormalizeX26xProgressPrefix(string line)
    {
        if (line.StartsWith("x264 ", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("x265 ", StringComparison.OrdinalIgnoreCase))
        {
            var bracketIndex = line.IndexOf('[');
            if (bracketIndex > 0)
            {
                return line[bracketIndex..].TrimStart();
            }
        }

        return line;
    }
}
