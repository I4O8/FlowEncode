using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlowEncode.Application;

namespace FlowEncode.Infrastructure;

public sealed class VapourSynthWorkspaceService : IVapourSynthWorkspaceService
{
    private static readonly JsonSerializerOptions SessionSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _sessionPath;

    public VapourSynthWorkspaceService(LocalAppPaths appPaths)
    {
        var workspaceRootPath = Path.Combine(appPaths.DataRootPath, "vapoursynth-workspace");
        Directory.CreateDirectory(workspaceRootPath);

        _sessionPath = Path.Combine(workspaceRootPath, "editor-session.json");
        EditorAssetsRootPath = Path.Combine(AppContext.BaseDirectory, "Assets", "VapourSynthEditor");
    }

    public string EditorAssetsRootPath { get; }

    public Task<VapourSynthWorkspaceDocument> CreateNewDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new VapourSynthWorkspaceDocument(null, BuildStarterScript()));
    }

    public async Task<VapourSynthWorkspaceDocument> OpenDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        filePath = Path.GetFullPath(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return new VapourSynthWorkspaceDocument(filePath, content);
    }

    public async Task<VapourSynthWorkspaceDocument> SaveDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        filePath = Path.GetFullPath(filePath);
        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(filePath, content ?? string.Empty, new UTF8Encoding(false), cancellationToken);
        return new VapourSynthWorkspaceDocument(filePath, content ?? string.Empty);
    }

    public async Task<VapourSynthWorkspaceSession?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_sessionPath))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var json = await File.ReadAllTextAsync(_sessionPath, Encoding.UTF8, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var dto = JsonSerializer.Deserialize<WorkspaceSessionDto>(json, SessionSerializerOptions);
        if (dto is null)
        {
            return null;
        }

        return new VapourSynthWorkspaceSession(
            string.IsNullOrWhiteSpace(dto.ActiveFilePath) ? null : dto.ActiveFilePath,
            dto.ActiveContent,
            dto.IsDirty,
            NormalizeRecentFiles(dto.RecentFiles),
            string.IsNullOrWhiteSpace(dto.ActiveSavedContentHash) ? null : dto.ActiveSavedContentHash);
    }

    public async Task SaveSessionAsync(VapourSynthWorkspaceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var dto = new WorkspaceSessionDto
        {
            ActiveFilePath = session.ActiveFilePath,
            ActiveContent = session.ActiveContent,
            IsDirty = session.IsDirty,
            RecentFiles = NormalizeRecentFiles(session.RecentFiles).ToList(),
            ActiveSavedContentHash = session.ActiveSavedContentHash
        };

        var json = JsonSerializer.Serialize(dto, SessionSerializerOptions);
        cancellationToken.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(_sessionPath, json, new UTF8Encoding(false), cancellationToken);
    }

    private static IReadOnlyList<string> NormalizeRecentFiles(IEnumerable<string>? recentFiles)
    {
        return (recentFiles ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static string BuildStarterScript()
    {
        return string.Join(
            Environment.NewLine,
            [
                "import vapoursynth as vs",
                string.Empty,
                "core = vs.core",
                string.Empty,
                "clip = core.std.BlankClip(",
                "    format=vs.YUV420P8,",
                "    width=1920,",
                "    height=1080,",
                "    length=240,",
                "    fpsnum=24000,",
                "    fpsden=1001)",
                string.Empty,
                "clip.set_output()",
                string.Empty
            ]);
    }

    private sealed class WorkspaceSessionDto
    {
        public string? ActiveFilePath { get; set; }

        public string? ActiveContent { get; set; }

        public bool IsDirty { get; set; }

        public List<string> RecentFiles { get; set; } = [];

        public string? ActiveSavedContentHash { get; set; }
    }
}
