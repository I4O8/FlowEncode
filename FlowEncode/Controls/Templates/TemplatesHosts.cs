using FlowEncode.Domain;
using FlowEncode.ViewModels;

namespace FlowEncode.Controls.Templates;

internal interface ITemplatesViewHost
{
    void SetOverviewTemplateSelection(TemplateLibraryItemViewModel? templateItem);

    void SetSavedTemplateQuickSelection(SavedTemplate? template);
}
