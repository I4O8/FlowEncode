namespace FlowEncode.Domain;

public sealed record BluRayPlaylistItem(
    string Id,
    string DisplayName,
    string Summary,
    string SourcePath,
    string SelectionToken,
    string DurationText,
    TimeSpan? Duration,
    int? ChapterCount);
