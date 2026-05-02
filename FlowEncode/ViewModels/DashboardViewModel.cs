namespace FlowEncode.ViewModels;

public sealed class DashboardViewModel : ModuleViewModelBase
{
    public DashboardViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public AppText Texts => Owner.Texts;
}
