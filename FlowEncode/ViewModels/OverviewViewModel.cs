namespace FlowEncode.ViewModels;

public sealed class OverviewViewModel : ModuleViewModelBase
{
    public OverviewViewModel(MainWindowViewModel owner)
        : base(owner)
    {
        Composer = TrackDisposable(new OverviewComposerViewModel(owner));
        Queue = TrackDisposable(new OverviewQueueViewModel(owner));
    }

    public AppText Texts => Owner.Texts;

    public OverviewComposerViewModel Composer { get; }

    public OverviewQueueViewModel Queue { get; }
}
