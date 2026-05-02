using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FlowEncode.ViewModels;

public sealed class AutoCompressionStatusViewModel : ModuleViewModelBase
{
    public AutoCompressionStatusViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public AppText Texts => Owner.Texts;

    public Brush AutoCompressionStatusPanelBorderBrush => Owner.AutoCompressionStatusPanelBorderBrush;

    public string AutoCompressionStatusText => Owner.AutoCompressionStatusText;

    public double AutoCompressionProgressPercent => Owner.AutoCompressionProgressPercent;

    public bool AutoCompressionProgressIsIndeterminate => Owner.AutoCompressionProgressIsIndeterminate;

    public string AutoCompressionProgressLabel => Owner.AutoCompressionProgressLabel;

    public string AutoCompressionProgressHint => Owner.AutoCompressionProgressHint;

    public Visibility AutoCompressionProgressHintVisibility => Owner.AutoCompressionProgressHintVisibility;

    public string AutoCompressionCommandLine => Owner.AutoCompressionCommandLine;

    public string AutoCompressionLog => Owner.AutoCompressionLog;
}
