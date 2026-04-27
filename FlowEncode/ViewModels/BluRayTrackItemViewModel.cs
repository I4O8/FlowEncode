using FlowEncode.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FlowEncode.ViewModels;

public sealed class BluRayTrackItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _outputPreview;

    public BluRayTrackItemViewModel(BluRayTrackItem track)
    {
        Track = track;
        _isSelected = track.IsSelectedByDefault;
        _outputPreview = string.Empty;
    }

    public BluRayTrackItem Track { get; }

    public string Title => Track.DisplayName;

    public string Subtitle => Track.Detail;

    public string Language => Track.Language;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string OutputPreview
    {
        get => _outputPreview;
        set => SetProperty(ref _outputPreview, value);
    }
}
