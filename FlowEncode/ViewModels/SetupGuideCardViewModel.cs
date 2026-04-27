using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace FlowEncode.ViewModels;

public sealed class SetupGuideCardViewModel
{
    public SetupGuideCardViewModel(
        string title,
        string description,
        string summary,
        IEnumerable<SetupGuideDependencyItemViewModel> items)
    {
        Title = title;
        Description = description;
        Summary = summary;
        Items = new ObservableCollection<SetupGuideDependencyItemViewModel>(items);
    }

    public string Title { get; }

    public string Description { get; }

    public string Summary { get; }

    public ObservableCollection<SetupGuideDependencyItemViewModel> Items { get; }
}
