using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IProfileLibraryService
{
    Task<IReadOnlyList<SavedTemplate>> GetUserTemplatesAsync(CancellationToken cancellationToken = default);

    Task<SavedTemplate> ReadTemplateAsync(string filePath, CancellationToken cancellationToken = default);

    Task ExportTemplateAsync(SavedTemplate template, string filePath, CancellationToken cancellationToken = default);

    Task<SavedTemplate> SaveTemplateAsync(
        string name,
        string notes,
        EncodingProfile profile,
        string? templateId = null,
        bool isPinned = false,
        CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);

    Task<SavedTemplate> SetTemplatePinnedAsync(
        string templateId,
        bool isPinned,
        CancellationToken cancellationToken = default);

    Task<CommandPreview> BuildPreviewAsync(EncodingProfile profile, CancellationToken cancellationToken = default);
}
