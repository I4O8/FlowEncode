using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace FlowEncode.ViewModels;

public partial class MainWindowViewModel
{
    private readonly HashSet<string> _loadedShellSections = new(StringComparer.Ordinal)
    {
        MainShellSections.Dashboard,
        MainShellSections.Overview
    };

    private string _activeShellSectionTag = MainShellSections.Dashboard;

    public string ActiveShellSectionTag
    {
        get => _activeShellSectionTag;
        private set
        {
            if (!SetProperty(ref _activeShellSectionTag, value))
            {
                return;
            }

            RaiseShellSectionVisibilityPropertyChanges();
        }
    }

    public bool IsDashboardSectionLoaded => IsShellSectionLoaded(MainShellSections.Dashboard);

    public bool IsBluRayDemuxSectionLoaded => IsShellSectionLoaded(MainShellSections.BluRayDemux);

    public bool IsVapourSynthWorkspaceSectionLoaded => IsShellSectionLoaded(MainShellSections.VapourSynthWorkspace);

    public bool IsOverviewSectionLoaded => IsShellSectionLoaded(MainShellSections.Overview);

    public bool IsTemplatesSectionLoaded => IsShellSectionLoaded(MainShellSections.Templates);

    public bool IsAudioProcessingSectionLoaded => IsShellSectionLoaded(MainShellSections.AudioProcessing);

    public bool IsAutoCompressionSectionLoaded => IsShellSectionLoaded(MainShellSections.AutoCompression);

    public bool IsSettingsSectionLoaded => IsShellSectionLoaded(MainShellSections.Settings);

    public Visibility DashboardSectionVisibility => GetShellSectionVisibility(MainShellSections.Dashboard);

    public Visibility BluRayDemuxSectionVisibility => GetShellSectionVisibility(MainShellSections.BluRayDemux);

    public Visibility VapourSynthWorkspaceSectionVisibility => GetShellSectionVisibility(MainShellSections.VapourSynthWorkspace);

    public Visibility OverviewSectionVisibility => GetShellSectionVisibility(MainShellSections.Overview);

    public Visibility TemplatesSectionVisibility => GetShellSectionVisibility(MainShellSections.Templates);

    public Visibility AudioProcessingSectionVisibility => GetShellSectionVisibility(MainShellSections.AudioProcessing);

    public Visibility AutoCompressionSectionVisibility => GetShellSectionVisibility(MainShellSections.AutoCompression);

    public Visibility SettingsSectionVisibility => GetShellSectionVisibility(MainShellSections.Settings);

    public bool ActivateShellSection(string? tag)
    {
        var normalizedTag = MainShellSections.Normalize(tag);
        var wasLoaded = _loadedShellSections.Contains(normalizedTag);
        if (!wasLoaded)
        {
            _loadedShellSections.Add(normalizedTag);
            RaiseShellSectionLoadedPropertyChange(normalizedTag);
        }

        ActiveShellSectionTag = normalizedTag;
        return !wasLoaded;
    }

    private bool IsShellSectionLoaded(string tag)
    {
        return _loadedShellSections.Contains(tag);
    }

    private Visibility GetShellSectionVisibility(string tag)
    {
        return string.Equals(ActiveShellSectionTag, tag, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RaiseShellSectionLoadedPropertyChange(string tag)
    {
        switch (tag)
        {
            case MainShellSections.Dashboard:
                OnPropertyChanged(nameof(IsDashboardSectionLoaded));
                break;
            case MainShellSections.BluRayDemux:
                OnPropertyChanged(nameof(IsBluRayDemuxSectionLoaded));
                break;
            case MainShellSections.VapourSynthWorkspace:
                OnPropertyChanged(nameof(IsVapourSynthWorkspaceSectionLoaded));
                break;
            case MainShellSections.Overview:
                OnPropertyChanged(nameof(IsOverviewSectionLoaded));
                break;
            case MainShellSections.Templates:
                OnPropertyChanged(nameof(IsTemplatesSectionLoaded));
                break;
            case MainShellSections.AudioProcessing:
                OnPropertyChanged(nameof(IsAudioProcessingSectionLoaded));
                break;
            case MainShellSections.AutoCompression:
                OnPropertyChanged(nameof(IsAutoCompressionSectionLoaded));
                break;
            case MainShellSections.Settings:
                OnPropertyChanged(nameof(IsSettingsSectionLoaded));
                break;
        }
    }

    private void RaiseShellSectionVisibilityPropertyChanges()
    {
        OnPropertyChanged(nameof(DashboardSectionVisibility));
        OnPropertyChanged(nameof(BluRayDemuxSectionVisibility));
        OnPropertyChanged(nameof(VapourSynthWorkspaceSectionVisibility));
        OnPropertyChanged(nameof(OverviewSectionVisibility));
        OnPropertyChanged(nameof(TemplatesSectionVisibility));
        OnPropertyChanged(nameof(AudioProcessingSectionVisibility));
        OnPropertyChanged(nameof(AutoCompressionSectionVisibility));
        OnPropertyChanged(nameof(SettingsSectionVisibility));
    }
}
