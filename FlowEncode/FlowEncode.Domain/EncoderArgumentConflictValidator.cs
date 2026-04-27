using System.Text.RegularExpressions;

namespace FlowEncode.Domain;

public enum EncoderArgumentSource
{
    AdditionalArguments,
    UhdParameters
}

public enum EncoderArgumentConflictKind
{
    ConflictingValues,
    OppositeSwitches
}

public sealed record EncoderArgumentConflict(
    EncoderArgumentConflictKind Kind,
    string OptionName,
    string FirstOptionName,
    string? FirstValue,
    EncoderArgumentSource FirstSource,
    string SecondOptionName,
    string? SecondValue,
    EncoderArgumentSource SecondSource);

public static partial class EncoderArgumentConflictValidator
{
    public static EncoderArgumentConflict? FindFirstConflict(
        EncoderKind kind,
        string? additionalArguments,
        string? uhdParameters)
    {
        var occurrences = new List<ArgumentOccurrence>();
        AddOccurrences(occurrences, additionalArguments, EncoderArgumentSource.AdditionalArguments);

        if (kind == EncoderKind.X265)
        {
            AddOccurrences(occurrences, uhdParameters, EncoderArgumentSource.UhdParameters);
        }

        if (occurrences.Count < 2)
        {
            return null;
        }

        var orderedGroups = occurrences
            .GroupBy(static occurrence => occurrence.OptionName, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.OrderBy(static occurrence => occurrence.Position).ToList())
            .OrderBy(static group => group[0].Position);

        foreach (var group in orderedGroups)
        {
            var conflict = FindConflict(group);
            if (conflict is not null)
            {
                return conflict;
            }
        }

        return null;
    }

    private static EncoderArgumentConflict? FindConflict(IReadOnlyList<ArgumentOccurrence> occurrences)
    {
        for (var leftIndex = 0; leftIndex < occurrences.Count - 1; leftIndex++)
        {
            var left = occurrences[leftIndex];

            for (var rightIndex = leftIndex + 1; rightIndex < occurrences.Count; rightIndex++)
            {
                var right = occurrences[rightIndex];
                if (!AreConflicting(left, right))
                {
                    continue;
                }

                var kind = IsOppositeSwitchPair(left, right)
                    ? EncoderArgumentConflictKind.OppositeSwitches
                    : EncoderArgumentConflictKind.ConflictingValues;

                return new EncoderArgumentConflict(
                    kind,
                    left.OptionName,
                    left.RawOptionName,
                    left.DisplayValue,
                    left.Source,
                    right.RawOptionName,
                    right.DisplayValue,
                    right.Source);
            }
        }

        return null;
    }

    private static bool AreConflicting(ArgumentOccurrence left, ArgumentOccurrence right)
    {
        if (left.BooleanValue.HasValue && right.BooleanValue.HasValue)
        {
            return left.BooleanValue.Value != right.BooleanValue.Value;
        }

        if (left.BooleanValue.HasValue || right.BooleanValue.HasValue)
        {
            return false;
        }

        return left.NormalizedValue is not null
            && right.NormalizedValue is not null
            && !string.Equals(left.NormalizedValue, right.NormalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOppositeSwitchPair(ArgumentOccurrence left, ArgumentOccurrence right)
    {
        return IsNegativeOption(left.RawOptionName) || IsNegativeOption(right.RawOptionName);
    }

    private static bool IsNegativeOption(string optionName)
    {
        return optionName.StartsWith("--no-", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddOccurrences(
        ICollection<ArgumentOccurrence> occurrences,
        string? arguments,
        EncoderArgumentSource source)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return;
        }

        var tokens = Tokenize(arguments);
        for (var index = 0; index < tokens.Count; index++)
        {
            if (!TryReadOption(tokens, ref index, out var optionName, out var value))
            {
                continue;
            }

            if (TryCreateOccurrence(optionName, value, source, occurrences.Count, out var occurrence))
            {
                occurrences.Add(occurrence);
            }
        }
    }

    private static bool TryReadOption(
        IReadOnlyList<string> tokens,
        ref int index,
        out string optionName,
        out string? value)
    {
        optionName = string.Empty;
        value = null;

        var token = tokens[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryParseInlineOption(token, out optionName, out value))
        {
            return true;
        }

        optionName = token;
        if (TryReadValue(tokens, index, out var explicitValue))
        {
            value = explicitValue;
            index++;
        }

        return true;
    }

    private static bool TryCreateOccurrence(
        string optionName,
        string? value,
        EncoderArgumentSource source,
        int position,
        out ArgumentOccurrence occurrence)
    {
        occurrence = default!;

        var normalizedOptionName = optionName.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedOptionName))
        {
            return false;
        }

        var displayValue = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        var canonicalOptionName = normalizedOptionName;
        bool? booleanValue = null;
        string? normalizedValue = displayValue;

        if (normalizedOptionName.StartsWith("--no-", StringComparison.Ordinal))
        {
            var baseName = normalizedOptionName[5..];
            if (string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            canonicalOptionName = $"--{baseName}";
            booleanValue = false;
            normalizedValue = "0";
        }
        else if (displayValue is null)
        {
            booleanValue = true;
            normalizedValue = "1";
        }
        else if (TryParseBooleanValue(displayValue, out var parsedBoolean))
        {
            booleanValue = parsedBoolean;
            normalizedValue = parsedBoolean ? "1" : "0";
        }

        occurrence = new ArgumentOccurrence(
            canonicalOptionName,
            normalizedOptionName,
            displayValue,
            normalizedValue,
            booleanValue,
            source,
            position);
        return true;
    }

    private static bool TryParseInlineOption(string token, out string optionName, out string value)
    {
        optionName = string.Empty;
        value = string.Empty;

        var separatorIndex = token.IndexOf('=');
        if (separatorIndex <= 2)
        {
            return false;
        }

        optionName = token[..separatorIndex];
        value = token[(separatorIndex + 1)..];
        return optionName.StartsWith("--", StringComparison.Ordinal);
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

    private static bool TryParseBooleanValue(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "on":
            case "yes":
                result = true;
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
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

    private sealed record ArgumentOccurrence(
        string OptionName,
        string RawOptionName,
        string? DisplayValue,
        string? NormalizedValue,
        bool? BooleanValue,
        EncoderArgumentSource Source,
        int Position);
}
