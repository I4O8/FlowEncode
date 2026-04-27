namespace FlowEncode.Domain;

public sealed record BluRayTrackItem(
    string Id,
    int Order,
    string DemuxToken,
    BluRayTrackKind Kind,
    string DisplayName,
    string Detail,
    string Language,
    bool IsSelectedByDefault = false);
