namespace FlowEncode.ViewModels;

public sealed class AutoCompressionViewModel : ModuleViewModelBase
{
    public AutoCompressionViewModel(MainWindowViewModel owner)
        : base(owner)
    {
        Form = TrackDisposable(new AutoCompressionFormViewModel(owner));
        Status = TrackDisposable(new AutoCompressionStatusViewModel(owner));
    }

    public AutoCompressionFormViewModel Form { get; }

    public AutoCompressionStatusViewModel Status { get; }
}
