using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

internal sealed partial class SourceVideoInfoProbe
{
    private readonly ExternalToolLocator _toolLocator;

    public SourceVideoInfoProbe(ExternalToolLocator toolLocator)
    {
        _toolLocator = toolLocator;
    }

    public SourceVideoInfo? Probe(string sourcePath, InputPipelineKind pipelineKind)
    {
        return pipelineKind switch
        {
            InputPipelineKind.VapourSynth => ProbeVapourSynth(sourcePath),
            InputPipelineKind.Y4mFile => ProbeY4m(sourcePath),
            InputPipelineKind.FfmpegPipe => ProbeFfprobe(sourcePath),
            InputPipelineKind.RawYuvFile => null,
            _ => null
        };
    }

    private SourceVideoInfo ProbeVapourSynth(string sourcePath)
    {
        var executablePath = _toolLocator.ResolveVspipe();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--info {Quote(sourcePath)}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        VapourSynthRuntimePathResolver.EnrichProcessPath(process.StartInfo);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"vspipe --info 失败：{FirstMeaningfulLine(error)}");
        }

        return ParseVspipeInfo(output);
    }

    private static SourceVideoInfo ProbeY4m(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        var header = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("YUV4MPEG2", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Y4M 头信息无效，无法探测源信息。");
        }

        var width = 0;
        var height = 0;
        var bitDepth = 8;
        var fpsNumerator = default(int?);
        var fpsDenominator = default(int?);
        var pixelFormat = "YUV420P8";

        foreach (var token in header.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 2)
            {
                continue;
            }

            switch (token[0])
            {
                case 'W':
                    width = ParseInt(token[1..], "Y4M 宽度");
                    break;
                case 'H':
                    height = ParseInt(token[1..], "Y4M 高度");
                    break;
                case 'F':
                    (fpsNumerator, fpsDenominator) = ParseY4mFps(token[1..]);
                    break;
                case 'C':
                    bitDepth = ParseY4mBitDepth(token[1..]);
                    pixelFormat = NormalizeY4mPixelFormat(token[1..], bitDepth);
                    break;
            }
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Y4M 头信息缺少宽高字段。");
        }

        return new SourceVideoInfo(width, height, null, bitDepth, fpsNumerator, fpsDenominator, pixelFormat);
    }

    private SourceVideoInfo ProbeFfprobe(string sourcePath)
    {
        var output = RunFfprobe(
            $"-v error -select_streams v:0 -show_entries stream=width,height,avg_frame_rate,r_frame_rate,nb_frames,pix_fmt,bits_per_raw_sample,duration:format=duration -of json {Quote(sourcePath)}");

        return ParseFfprobeInfo(output);
    }

    private string RunFfprobe(string arguments)
    {
        var executablePath = _toolLocator.ResolveFfprobe();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        VapourSynthRuntimePathResolver.EnrichProcessPath(process.StartInfo);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe 探测失败：{FirstMeaningfulLine(error)}");
        }

        return output;
    }

    private static SourceVideoInfo ParseVspipeInfo(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = InfoLineRegex().Match(rawLine);
            if (!match.Success)
            {
                continue;
            }

            values[match.Groups["key"].Value.Trim()] = match.Groups["value"].Value.Trim();
        }

        var width = ParseInt(values.TryGetValue("Width", out var widthValue) ? widthValue : string.Empty, "Width");
        var height = ParseInt(values.TryGetValue("Height", out var heightValue) ? heightValue : string.Empty, "Height");
        var frames = ParseLong(values.TryGetValue("Frames", out var framesValue) ? framesValue : string.Empty);
        var pixelFormat = values.TryGetValue("Format Name", out var formatValue) ? formatValue : "Unknown";
        var bitDepth = values.TryGetValue("Bits", out var bitsValue)
            ? ParseInt(bitsValue, "Bits")
            : ParseBitDepthFromFormat(pixelFormat);
        var (fpsNumerator, fpsDenominator) = ParseVspipeFps(values.TryGetValue("FPS", out var fpsValue) ? fpsValue : string.Empty);

        return new SourceVideoInfo(width, height, frames, bitDepth, fpsNumerator, fpsDenominator, pixelFormat);
    }

    private static SourceVideoInfo ParseFfprobeInfo(string output)
    {
        using var document = JsonDocument.Parse(output);
        if (!document.RootElement.TryGetProperty("streams", out var streams)
            || streams.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("ffprobe 未返回可用的视频流信息。");
        }

        JsonElement? streamElement = null;
        foreach (var stream in streams.EnumerateArray())
        {
            streamElement = stream;
            break;
        }

        if (!streamElement.HasValue)
        {
            throw new InvalidOperationException("ffprobe 未找到视频流。");
        }

        var streamInfo = streamElement.Value;
        var width = ParseInt(GetJsonString(streamInfo, "width"), "Width");
        var height = ParseInt(GetJsonString(streamInfo, "height"), "Height");
        var pixelFormat = GetJsonString(streamInfo, "pix_fmt");
        var bitDepth = ParseBitDepthFromFfprobe(streamInfo, pixelFormat);
        var (fpsNumerator, fpsDenominator) = ParseFfprobeRate(
            GetJsonString(streamInfo, "avg_frame_rate"),
            GetJsonString(streamInfo, "r_frame_rate"));
        var durationSeconds = ParseInvariantDoubleNullable(GetJsonString(streamInfo, "duration"))
            ?? ParseFfprobeFormatDuration(document.RootElement);
        var totalFrames = ParseFfprobeFrameCount(GetJsonString(streamInfo, "nb_frames"), fpsNumerator, fpsDenominator, durationSeconds);

        return new SourceVideoInfo(
            width,
            height,
            totalFrames,
            bitDepth,
            fpsNumerator,
            fpsDenominator,
            string.IsNullOrWhiteSpace(pixelFormat) ? "unknown" : pixelFormat);
    }

    private static (int? Numerator, int? Denominator) ParseVspipeFps(string value)
    {
        var match = FractionRegex().Match(value);
        if (!match.Success)
        {
            return (null, null);
        }

        return (ParseInt(match.Groups["num"].Value, "FPS numerator"), ParseInt(match.Groups["den"].Value, "FPS denominator"));
    }

    private static (int? Numerator, int? Denominator) ParseFfprobeRate(string primaryValue, string fallbackValue)
    {
        var primaryRate = ParseFraction(primaryValue);
        if (primaryRate.Numerator is > 0 && primaryRate.Denominator is > 0)
        {
            return primaryRate;
        }

        return ParseFraction(fallbackValue);
    }

    private static (int? Numerator, int? Denominator) ParseY4mFps(string value)
    {
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return (null, null);
        }

        return (ParseInt(parts[0], "Y4M FPS numerator"), ParseInt(parts[1], "Y4M FPS denominator"));
    }

    private static int ParseY4mBitDepth(string colorDescriptor)
    {
        var match = BitDepthRegex().Match(colorDescriptor);
        if (match.Success)
        {
            return ParseInt(match.Groups["bits"].Value, "Y4M bit depth");
        }

        return 8;
    }

    private static string NormalizeY4mPixelFormat(string colorDescriptor, int bitDepth)
    {
        var normalized = colorDescriptor.ToUpperInvariant();

        if (normalized.Contains("420", StringComparison.Ordinal))
        {
            return $"YUV420P{bitDepth}";
        }

        if (normalized.Contains("422", StringComparison.Ordinal))
        {
            return $"YUV422P{bitDepth}";
        }

        if (normalized.Contains("444", StringComparison.Ordinal))
        {
            return $"YUV444P{bitDepth}";
        }

        return $"YUV{bitDepth}";
    }

    private static int ParseBitDepthFromFormat(string formatName)
    {
        var match = BitDepthRegex().Match(formatName);
        return match.Success ? ParseInt(match.Groups["bits"].Value, "Format bit depth") : 8;
    }

    private static int ParseBitDepthFromFfprobe(JsonElement streamInfo, string pixelFormat)
    {
        var rawValue = GetJsonString(streamInfo, "bits_per_raw_sample");
        if (!string.IsNullOrWhiteSpace(rawValue)
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return ParseBitDepthFromFormat(pixelFormat);
    }

    private static (int? Numerator, int? Denominator) ParseFraction(string value)
    {
        var match = FractionRegex().Match(value);
        if (!match.Success)
        {
            return (null, null);
        }

        return (ParseInt(match.Groups["num"].Value, "rate numerator"), ParseInt(match.Groups["den"].Value, "rate denominator"));
    }

    private static int ParseInt(string value, string fieldName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"无法解析 {fieldName}：{value}");
    }

    private static long? ParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? ParseInvariantDoubleNullable(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ParseFfprobeFrameCount(
        string frameCountValue,
        int? fpsNumerator,
        int? fpsDenominator,
        double? durationSeconds)
    {
        var frameCount = ParseLong(frameCountValue);
        if (frameCount is > 0)
        {
            return frameCount;
        }

        if (durationSeconds is > 0
            && fpsNumerator is > 0
            && fpsDenominator is > 0)
        {
            var totalFrames = durationSeconds.Value * fpsNumerator.Value / fpsDenominator.Value;
            return (long)Math.Round(totalFrames, MidpointRounding.AwayFromZero);
        }

        return null;
    }

    private static double? ParseFfprobeFormatDuration(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("format", out var formatElement)
            || formatElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ParseInvariantDoubleNullable(GetJsonString(formatElement, "duration"));
    }

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            _ => property.GetRawText().Trim('"')
        };
    }

    private static string FirstMeaningfulLine(string output)
    {
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line))
            ?? "未返回可读的错误信息。";
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    [GeneratedRegex(@"^(?<key>[^:]+):\s*(?<value>.+)$", RegexOptions.Compiled)]
    private static partial Regex InfoLineRegex();

    [GeneratedRegex(@"(?<num>\d+)\s*/\s*(?<den>\d+)", RegexOptions.Compiled)]
    private static partial Regex FractionRegex();

    [GeneratedRegex(@"(?<bits>8|9|10|12|14|16)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BitDepthRegex();
}
