namespace FlowEncode.ViewModels;

public sealed class BluRayDemuxViewModel : ModuleViewModelBase
{
    public BluRayDemuxViewModel(MainWindowViewModel owner)
        : base(owner)
    {
        Disc = TrackDisposable(new BluRayDemuxDiscViewModel(owner));
        Task = TrackDisposable(new BluRayDemuxTaskViewModel(owner));
    }

    public AppText Texts => Owner.Texts;

    public BluRayDemuxDiscViewModel Disc { get; }

    public BluRayDemuxTaskViewModel Task { get; }

    public void InitializeState()
    {
        Disc.InitializeState();
    }

    public void HandleEnvironmentReadinessApplied()
    {
        Disc.HandleEnvironmentReadinessApplied();
    }

    public void ApplyLanguageState()
    {
        Disc.ApplyLanguageState();
    }
}
