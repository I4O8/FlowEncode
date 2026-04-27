namespace FlowEncode.Domain;

public sealed record SavedTemplate(
    string Id,
    string Name,
    string Notes,
    EncodingProfile Profile,
    DateTimeOffset UpdatedAt,
    bool IsPinned = false)
{
    public string UpdatedLabel => UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
