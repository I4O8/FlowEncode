namespace FlowEncode.Domain;

public sealed record BluRayPlaylistScanResult(
    BluRayDemuxBackend Backend,
    string DiscPath,
    BluRayPlaylistItem Playlist,
    IReadOnlyList<BluRayTrackItem> Tracks,
    string Summary,
    string RawOutput);
