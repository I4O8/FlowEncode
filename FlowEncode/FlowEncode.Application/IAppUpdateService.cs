using FlowEncode.Domain;

namespace FlowEncode.Application;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<string> DownloadInstallerAsync(
        AppUpdateCheckResult updateResult,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
