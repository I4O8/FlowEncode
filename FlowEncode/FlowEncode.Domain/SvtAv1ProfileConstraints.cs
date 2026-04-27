using System.Text.RegularExpressions;

namespace FlowEncode.Domain;

public static partial class SvtAv1ProfileConstraints
{
    public static bool HasTwoPassOverlayConflict(EncodingProfile profile)
    {
        return profile.Kind == EncoderKind.SvtAv1
            && profile.RateControl == RateControlMode.TwoPass
            && IsOverlayEnabled(profile.AdditionalArguments);
    }

    private static bool IsOverlayEnabled(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return false;
        }

        var enabled = false;
        var tokens = Tokenize(arguments);

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (TryParseInlineOption(token, "--enable-overlays", out var overlayValue))
            {
                enabled = IsTruthyValue(overlayValue);
                continue;
            }

            if (token.Equals("--enable-overlays", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadValue(tokens, index, out var explicitValue))
                {
                    enabled = IsTruthyValue(explicitValue);
                    index++;
                }
                else
                {
                    enabled = true;
                }

                continue;
            }

            if (TryParseInlineOption(token, "--svtav1-params", out var inlineParams))
            {
                ApplySvtParamOverrides(inlineParams, ref enabled);
                continue;
            }

            if (token.Equals("--svtav1-params", StringComparison.OrdinalIgnoreCase)
                && TryReadValue(tokens, index, out var parameterValue))
            {
                ApplySvtParamOverrides(parameterValue, ref enabled);
                index++;
            }
        }

        return enabled;
    }

    private static void ApplySvtParamOverrides(string parameterValue, ref bool enabled)
    {
        foreach (var entry in parameterValue.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = entry[..separatorIndex].Trim();
            var value = entry[(separatorIndex + 1)..].Trim();

            if (key.Equals("enable-overlays", StringComparison.OrdinalIgnoreCase))
            {
                enabled = IsTruthyValue(value);
            }
        }
    }

    private static bool TryParseInlineOption(string token, string optionName, out string value)
    {
        value = string.Empty;
        if (!token.StartsWith(optionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.Length == optionName.Length)
        {
            return false;
        }

        if (token[optionName.Length] != '=')
        {
            return false;
        }

        value = token[(optionName.Length + 1)..].Trim();
        return true;
    }

    private static bool TryReadValue(IReadOnlyList<string> tokens, int optionIndex, out string value)
    {
        value = string.Empty;
        if (optionIndex + 1 >= tokens.Count)
        {
            return false;
        }

        value = tokens[optionIndex + 1];
        return !IsOptionToken(value);
    }

    private static bool IsOptionToken(string value)
    {
        return value.StartsWith("--", StringComparison.Ordinal)
            || (value.StartsWith("-", StringComparison.Ordinal)
                && value.Length > 1
                && char.IsLetter(value[1]));
    }

    private static bool IsTruthyValue(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "0" or "false" or "off" or "no" => false,
            _ => true
        };
    }

    private static List<string> Tokenize(string value)
    {
        return TokenRegex()
            .Matches(value)
            .Select(match => match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Success
                    ? match.Groups[2].Value
                    : match.Groups[3].Value)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    [GeneratedRegex("\"([^\"]*)\"|'([^']*)'|(\\S+)")]
    private static partial Regex TokenRegex();
}
