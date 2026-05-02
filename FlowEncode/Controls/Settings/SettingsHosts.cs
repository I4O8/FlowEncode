using System.Threading.Tasks;

namespace FlowEncode.Controls.Settings;

internal interface ISettingsViewHost
{
    Task<bool> PersistSettingsAsync(bool refreshTemplateLibrary);

    Task HandleAppUpdateAsync();

    Task OpenSetupGuideAsync();
}

internal interface IShellNavigationHost
{
    void NavigateToShellSection(string tag);
}
