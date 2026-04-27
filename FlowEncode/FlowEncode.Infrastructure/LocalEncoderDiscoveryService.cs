using FlowEncode.Application;
using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

public sealed class LocalEncoderDiscoveryService : IEncoderDiscoveryService
{
    private static readonly IReadOnlyDictionary<EncoderKind, string[]> EnvironmentVariableNames = new Dictionary<EncoderKind, string[]>
    {
        [EncoderKind.X264] = ["FLOWENCODE_X264", "X264_PATH", "X264_EXE", "X264"],
        [EncoderKind.X265] = ["FLOWENCODE_X265", "X265_PATH", "X265_EXE", "X265"],
        [EncoderKind.SvtAv1] = ["FLOWENCODE_AV1", "SVT_AV1_PATH", "SVTAV1_PATH", "SVTAV1", "AV1_ENCODER", "AV1"]
    };

    private static readonly IReadOnlyDictionary<EncoderKind, string[]> ExecutableNames = new Dictionary<EncoderKind, string[]>
    {
        [EncoderKind.X264] = ["x264.exe"],
        [EncoderKind.X265] = ["x265.exe"],
        [EncoderKind.SvtAv1] = ["SvtAv1EncApp.exe", "svt-av1.exe"]
    };

    private readonly LocalAppPaths _paths;

    public LocalEncoderDiscoveryService(LocalAppPaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<DiscoveredEncoderBinary> DiscoverSystemBinaries()
    {
        var results = new List<DiscoveredEncoderBinary>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kind in Enum.GetValues<EncoderKind>())
        {
            foreach (var variableName in EnvironmentVariableNames[kind])
            {
                var value = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process)
                    ?? Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User)
                    ?? Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);

                var resolvedPath = ResolveFromInput(value, kind);
                if (string.IsNullOrWhiteSpace(resolvedPath) || !seen.Add($"{kind}:{resolvedPath}"))
                {
                    continue;
                }

                results.Add(CreateCandidate(kind, resolvedPath, EncoderBinarySource.EnvironmentVariable, variableName));
            }

            foreach (var resolvedPath in EnumeratePathMatches(kind))
            {
                if (!seen.Add($"{kind}:{resolvedPath}"))
                {
                    continue;
                }

                results.Add(CreateCandidate(kind, resolvedPath, EncoderBinarySource.Path, "PATH"));
            }
        }

        return results
            .OrderBy(static item => item.Kind)
            .ThenBy(static item => item.Source)
            .ThenBy(static item => item.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public DiscoveredEncoderBinary? ResolveEncoder(
        EncoderKind kind,
        EncoderArchitecture preferredArchitecture,
        bool preferSystemEncoders)
    {
        var localCandidate = PickBest(GetLocalCandidates(kind), preferredArchitecture);
        if (localCandidate is not null)
        {
            return localCandidate;
        }

        if (!preferSystemEncoders)
        {
            return null;
        }

        return PickBest(DiscoverSystemBinaries().Where(candidate => candidate.Kind == kind), preferredArchitecture);
    }

    private IEnumerable<DiscoveredEncoderBinary> GetLocalCandidates(EncoderKind kind)
    {
        foreach (var architecture in Enum.GetValues<EncoderArchitecture>())
        {
            var path = _paths.GetBinaryPath(kind, architecture);
            if (!File.Exists(path))
            {
                continue;
            }

                yield return CreateCandidate(kind, path, EncoderBinarySource.LocalToolset, "encoders");
        }
    }

    private static DiscoveredEncoderBinary? PickBest(IEnumerable<DiscoveredEncoderBinary> candidates, EncoderArchitecture preferredArchitecture)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Architecture == preferredArchitecture)
            .ThenBy(candidate => candidate.Source)
            .FirstOrDefault();
    }

    private static DiscoveredEncoderBinary CreateCandidate(EncoderKind kind, string executablePath, EncoderBinarySource source, string sourceLabel)
    {
        return new DiscoveredEncoderBinary(
            kind,
            EncoderBinaryProbe.DetectArchitecture(executablePath),
            executablePath,
            source,
            sourceLabel,
            EncoderBinaryProbe.ProbeVersion(executablePath, kind));
    }

    private static IEnumerable<string> EnumeratePathMatches(EncoderKind kind)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (var root in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var fileName in ExecutableNames[kind])
            {
                var candidate = Path.Combine(root, fileName);
                if (File.Exists(candidate))
                {
                    yield return Path.GetFullPath(candidate);
                }
            }
        }
    }

    private static string? ResolveFromInput(string? value, EncoderKind kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().Trim('"');
        if (File.Exists(normalized))
        {
            return Path.GetFullPath(normalized);
        }

        if (Directory.Exists(normalized))
        {
            foreach (var fileName in ExecutableNames[kind])
            {
                var candidate = Path.Combine(normalized, fileName);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }

        if (!normalized.Contains(Path.DirectorySeparatorChar) && !normalized.Contains(Path.AltDirectorySeparatorChar))
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathVariable))
            {
                foreach (var root in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var candidate = Path.Combine(root, normalized);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
        }

        return null;
    }
}
