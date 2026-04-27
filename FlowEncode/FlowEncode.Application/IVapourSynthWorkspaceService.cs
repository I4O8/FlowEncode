using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlowEncode.Application;

public interface IVapourSynthWorkspaceService
{
    string EditorAssetsRootPath { get; }

    Task<VapourSynthWorkspaceDocument> CreateNewDocumentAsync(CancellationToken cancellationToken = default);

    Task<VapourSynthWorkspaceDocument> OpenDocumentAsync(string filePath, CancellationToken cancellationToken = default);

    Task<VapourSynthWorkspaceDocument> SaveDocumentAsync(string filePath, string content, CancellationToken cancellationToken = default);

    Task<VapourSynthWorkspaceSession?> LoadSessionAsync(CancellationToken cancellationToken = default);

    Task SaveSessionAsync(VapourSynthWorkspaceSession session, CancellationToken cancellationToken = default);
}

public sealed record VapourSynthWorkspaceDocument(
    string? FilePath,
    string Content);

public sealed record VapourSynthWorkspaceSession(
    string? ActiveFilePath,
    string? ActiveContent,
    bool IsDirty,
    IReadOnlyList<string> RecentFiles,
    string? ActiveSavedContentHash);
