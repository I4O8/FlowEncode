using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FlowEncode.Domain;
using Microsoft.UI.Xaml;

namespace FlowEncode.ViewModels;

public sealed class TemplateLibraryViewModel : ModuleViewModelBase
{
    public TemplateLibraryViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public AppText Texts => Owner.Texts;

    public ObservableCollection<TemplateLibraryItemViewModel> TemplateLibraryItems => Owner.TemplateLibraryItems;

    public string TemplateSearchText
    {
        get => Owner.TemplateSearchText;
        set => Owner.TemplateSearchText = value;
    }

    public Visibility TemplateLibraryEmptyVisibility => Owner.TemplateLibraryEmptyVisibility;

    public string? CurrentTemplateSelectionKey => Owner.CurrentTemplateSelectionKey;

    public Task SelectUserTemplateAsync(SavedTemplate? template)
    {
        return Owner.SelectUserTemplateAsync(template);
    }

    public Task<SavedTemplate> ReadTemplateAsync(string filePath)
    {
        return Owner.ReadTemplateAsync(filePath);
    }

    public Task<SavedTemplate> ImportTemplateAsync(SavedTemplate template, string? overwriteTemplateId = null)
    {
        return Owner.ImportTemplateAsync(template, overwriteTemplateId);
    }

    public SavedTemplate? FindUserTemplateByName(string? templateName)
    {
        return Owner.FindUserTemplateByName(templateName);
    }

    public SavedTemplate? FindUserTemplateById(string? templateId)
    {
        return Owner.FindUserTemplateById(templateId);
    }

    public Task DeleteTemplateAsync(string templateId)
    {
        return Owner.DeleteTemplateAsync(templateId);
    }

    public Task<SavedTemplate> SetTemplatePinnedAsync(string templateId, bool isPinned)
    {
        return Owner.SetTemplatePinnedAsync(templateId, isPinned);
    }
}
