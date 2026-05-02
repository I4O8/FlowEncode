namespace FlowEncode.ViewModels;

public sealed class AudioProcessingViewModel : ModuleViewModelBase
{
    public AudioProcessingViewModel(MainWindowViewModel owner)
        : base(owner)
    {
        Form = TrackDisposable(new AudioProcessingFormViewModel(owner));
        Status = TrackDisposable(new AudioProcessingStatusViewModel(owner));
    }

    public AppText Texts => Owner.Texts;

    public AudioProcessingFormViewModel Form { get; }

    public AudioProcessingStatusViewModel Status { get; }

    public void InitializeState()
    {
        Form.InitializeState();
    }

    public void HandleEnvironmentReadinessApplied()
    {
        Form.HandleEnvironmentReadinessApplied();
    }

    public void ApplyLanguageState()
    {
        Form.ApplyLanguageState();
    }
}
