using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed class SettingsViewModel : ModuleViewModelBase
{
    public SettingsViewModel(MainWindowViewModel owner)
        : base(owner)
    {
        General = TrackDisposable(new SettingsGeneralViewModel(owner));
        Dependencies = TrackDisposable(new SettingsDependenciesViewModel(owner));
    }

    public AppText Texts => Owner.Texts;

    public SettingsGeneralViewModel General { get; }

    public SettingsDependenciesViewModel Dependencies { get; }

    public AppThemePreference CurrentThemePreference => Owner.CurrentThemePreference;

    public AppLanguage CurrentLanguagePreference => Owner.CurrentLanguagePreference;

    public bool IsAppUpdateAvailable => Owner.IsAppUpdateAvailable;

    public bool CanDownloadAppUpdateInstaller => Owner.CanDownloadAppUpdateInstaller;

    public bool HasAppUpdateError => Owner.HasAppUpdateError;

    public string AppUpdateReleaseUrl => Owner.AppUpdateReleaseUrl;

    public string AppUpdateStatusText => Owner.AppUpdateStatusText;

    public Task<AppUpdateCheckResult?> RefreshAvailableUpdatesAsync(bool reportStatus = true)
    {
        return Owner.RefreshAvailableUpdatesAsync(reportStatus);
    }

    public Task<string?> DownloadLatestAppInstallerAsync(bool reportStatus = true)
    {
        return Owner.DownloadLatestAppInstallerAsync(reportStatus);
    }
}
