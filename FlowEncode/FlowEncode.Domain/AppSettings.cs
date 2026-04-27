namespace FlowEncode.Domain;

public sealed record AppSettings(
    bool PreferSystemEncoders,
    bool AutoCheckUpdatesOnStartup,
    AppThemePreference Theme,
    AppLanguage Language,
    bool HasSeenSetupGuide = false,
    string WorkspaceRootPath = "")
{
    public static AppSettings Default { get; } = new(
        PreferSystemEncoders: true,
        AutoCheckUpdatesOnStartup: true,
        Theme: AppThemePreference.Default,
        Language: AppLanguage.Chinese,
        HasSeenSetupGuide: false,
        WorkspaceRootPath: string.Empty);
}
