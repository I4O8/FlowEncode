using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

internal sealed record EncoderArgumentOverrides(
    RateControlMode? RateControl,
    double? Quality,
    int? Bitrate,
    string? Preset,
    string? Tune,
    string? Profile,
    string RemainingArguments);

internal static partial class EncoderArgumentOverrideParser
{
    [GeneratedRegex("\"([^\"]*)\"|'([^']*)'|(\\S+)")]
    private static partial Regex TokenRegex();

    public static EncoderArgumentOverrides Parse(EncoderKind kind, string? arguments, bool preserveRawSourceParameters = false)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return new EncoderArgumentOverrides(null, null, null, null, null, null, string.Empty);
        }

        var rateControl = default(RateControlMode?);
        var quality = default(double?);
        var bitrate = default(int?);
        string? preset = null;
        string? tune = null;
        string? profile = null;

        var tokens = Tokenize(arguments);
        var remaining = new List<string>();

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var normalized = token.ToLowerInvariant();

            if (TryReadValue(tokens, index, out var value))
            {
                switch (normalized)
                {
                    case "--progress":
                    case "--input":
                    case "--output":
                    case "--demuxer":
                    case "--stdin":
                    case "-o":
                        index++;
                        continue;
                    case "--input-depth":
                    case "--width":
                    case "--height":
                    case "--frames":
                    case "--fps":
                    case "--fps-num":
                    case "--fps-denom":
                    case "--input-res":
                    case "--input-csp":
                        if (!preserveRawSourceParameters)
                        {
                            index++;
                            continue;
                        }

                        break;
                    case "--y4m":
                        continue;
                    case "--preset":
                        preset = value;
                        index++;
                        continue;
                    case "--tune":
                        tune = EncoderArgumentValueNormalizer.NormalizeTuneForUi(kind, value);
                        index++;
                        continue;
                    case "--profile":
                    case "-p":
                    case "-p/--profile":
                        profile = EncoderArgumentValueNormalizer.NormalizeProfileForUi(kind, value);
                        index++;
                        continue;
                    case "--crf":
                        rateControl = RateControlMode.Crf;
                        quality = ParseDouble(value);
                        index++;
                        continue;
                    case "--cq":
                        rateControl = RateControlMode.Cq;
                        quality = ParseDouble(value);
                        index++;
                        continue;
                    case "--qp":
                        rateControl = kind == EncoderKind.SvtAv1 ? RateControlMode.Qp : RateControlMode.Cq;
                        quality = ParseDouble(value);
                        index++;
                        continue;
                    case "--bitrate":
                        rateControl = RateControlMode.Abr;
                        bitrate = ParseInt(value);
                        index++;
                        continue;
                    case "--tbr":
                        rateControl ??= RateControlMode.Vbr;
                        bitrate = ParseInt(value);
                        index++;
                        continue;
                    case "--passes":
                        if (ParseInt(value) >= 2)
                        {
                            rateControl = RateControlMode.TwoPass;
                        }

                        index++;
                        continue;
                    case "--pass":
                        if (ParseInt(value) >= 1)
                        {
                            rateControl = RateControlMode.TwoPass;
                        }

                        index++;
                        continue;
                    case "--rc":
                        if (kind == EncoderKind.SvtAv1 && ParseInt(value) == 1)
                        {
                            rateControl ??= RateControlMode.Vbr;
                        }

                        index++;
                        continue;
                }
            }

            remaining.Add(token);
        }

        return new EncoderArgumentOverrides(
            rateControl,
            quality,
            bitrate,
            preset,
            tune,
            profile,
            string.Join(" ", remaining));
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

    private static double ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
