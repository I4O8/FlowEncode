using System.Threading.Tasks;
using FlowEncode.ViewModels;

namespace FlowEncode.Controls.Overview;

internal interface IOverviewViewHost
{
    Task<bool> PersistSettingsAsync(bool refreshTemplateLibrary);

    void SetTemplateLibrarySelection(TemplateLibraryItemViewModel? templateItem);

    Task SaveCurrentTemplateAsync();
}
