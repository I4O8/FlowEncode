using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FlowEncode;

public sealed partial class MainWindow
{
    private const double BluRayPlaylistListMinHeight = 280;
    private const double BluRayPlaylistListMaxHeightCompact = 560;
    private const double BluRayPlaylistListMaxHeightWide = 1120;
    private const double BluRayTrackListMinHeight = 260;
    private const double BluRayTrackListMaxHeightCompact = 420;
    private const double BluRayTrackListMaxHeightWide = 620;

    private void InitializeBluRayDemuxInteractions()
    {
        BluRaySourcePathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(BluRaySourcePathTextBox_DoubleTapped), true);
        BluRayOutputPathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(BluRayOutputPathTextBox_DoubleTapped), true);
        BluRayDemuxSecondaryPanel.SizeChanged += BluRayDemuxSecondaryPanel_SizeChanged;
    }

    private void ApplyBluRayDemuxLayout(bool stackedWorkspace, bool compactForms)
    {
        BluRayDemuxWorkspaceGrid.ColumnSpacing = stackedWorkspace ? 0 : 20;
        BluRayDemuxWorkspaceGrid.RowSpacing = stackedWorkspace ? 20 : 0;
        BluRayDemuxPrimaryColumn.Width = new GridLength(stackedWorkspace ? 1 : 0.92, GridUnitType.Star);
        BluRayDemuxSecondaryColumn.Width = stackedWorkspace ? new GridLength(0) : new GridLength(1.08, GridUnitType.Star);
        BluRayDemuxWorkspacePrimaryRow.Height = GridLength.Auto;
        BluRayDemuxWorkspaceSecondaryRow.Height = stackedWorkspace ? GridLength.Auto : new GridLength(0);

        Grid.SetRow(BluRayDemuxPrimaryPanel, 0);
        Grid.SetColumn(BluRayDemuxPrimaryPanel, 0);
        Grid.SetColumnSpan(BluRayDemuxPrimaryPanel, 1);
        Grid.SetRow(BluRayDemuxSecondaryPanel, stackedWorkspace ? 1 : 0);
        Grid.SetColumn(BluRayDemuxSecondaryPanel, stackedWorkspace ? 0 : 1);
        Grid.SetColumnSpan(BluRayDemuxSecondaryPanel, stackedWorkspace ? 2 : 1);

        ConfigureTwoItemGrid(BluRaySourcePathGrid, BluRaySourcePathActionColumn, BluRaySourceBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(BluRayOutputPathGrid, BluRayOutputPathActionColumn, BluRayOutputBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(BluRayBackendActionGrid, BluRayBackendActionColumn, ScanBluRayButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(BluRayTrackHeaderGrid, BluRayTrackHeaderActionColumn, BluRayTrackSelectionActionsPanel, compactForms, GridLength.Auto);
        ConfigureThreeItemGrid(BluRayDemuxActionGrid, BluRayDemuxActionCancelColumn, BluRayDemuxActionClearColumn, CancelBluRayDemuxButton, ClearBluRayDemuxButton, compactForms);
        ApplyBluRayScrollableRegions(stackedWorkspace);
        ScheduleBluRayWorkspaceHeightRefresh(stackedWorkspace);
    }

    private void ApplyBluRayScrollableRegions(bool stackedWorkspace)
    {
        var rootHeight = RootLayout.ActualHeight;
        var playlistMaxHeight = stackedWorkspace
            ? (rootHeight > 0
                ? Math.Clamp(rootHeight * 0.44, BluRayPlaylistListMinHeight, BluRayPlaylistListMaxHeightCompact)
                : BluRayPlaylistListMaxHeightCompact)
            : double.PositiveInfinity;
        var trackMaxHeight = rootHeight > 0
            ? Math.Clamp(rootHeight * (stackedWorkspace ? 0.34 : 0.42), BluRayTrackListMinHeight, stackedWorkspace ? BluRayTrackListMaxHeightCompact : BluRayTrackListMaxHeightWide)
            : (stackedWorkspace ? BluRayTrackListMaxHeightCompact : BluRayTrackListMaxHeightWide);

        BluRayPlaylistListView.MaxHeight = playlistMaxHeight;
        BluRayTrackListView.MaxHeight = trackMaxHeight;
    }

    private void BluRayDemuxSecondaryPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isWindowReady)
        {
            return;
        }

        ScheduleBluRayWorkspaceHeightRefresh(RootLayout.ActualWidth < 1320);
    }

    private void ScheduleBluRayWorkspaceHeightRefresh(bool stackedWorkspace)
    {
        DispatcherQueue.TryEnqueue(() => UpdateBluRayWorkspaceHeight(stackedWorkspace));
    }

    private void UpdateBluRayWorkspaceHeight(bool stackedWorkspace)
    {
        if (stackedWorkspace || BluRayDemuxPanel.Visibility != Visibility.Visible)
        {
            ClearBluRayWorkspaceHeight();
            return;
        }

        if (BluRayDemuxSecondaryPanel.ActualHeight <= 0)
        {
            return;
        }

        BluRayDemuxPrimaryPanel.Height = Math.Ceiling(BluRayDemuxSecondaryPanel.ActualHeight);
    }

    private void ClearBluRayWorkspaceHeight()
    {
        BluRayDemuxPrimaryPanel.Height = double.NaN;
    }

    private async void BrowseBluRaySourceButton_Click(object sender, RoutedEventArgs e)
    {
        await PickBluRaySourceFolderAsync();
    }

    private async void BluRaySourcePathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickBluRaySourceFolderAsync();
    }

    private async Task PickBluRaySourceFolderAsync()
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is not null)
        {
            await ApplyPickedPathAsync(BluRaySourcePathTextBox, folderPath, path => ViewModel.BluRayDemuxSourcePath = path);
        }
    }

    private async void BrowseBluRayOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await PickBluRayOutputFolderAsync();
    }

    private async void BluRayOutputPathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickBluRayOutputFolderAsync();
    }

    private async Task PickBluRayOutputFolderAsync()
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is not null)
        {
            await ApplyPickedPathAsync(BluRayOutputPathTextBox, folderPath, path => ViewModel.BluRayDemuxOutputPath = path);
        }
    }

    private async void ScanBluRayButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ScanBluRayDiscAsync();
    }

    private async void BluRayPlaylistListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.LoadSelectedBluRayPlaylistAsync();
    }

    private async void StartBluRayDemuxButton_Click(object sender, RoutedEventArgs e)
    {
        var validationError = ViewModel.ValidateBluRayDemuxForStart();
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartBluRayDemuxTitle, validationError);
            return;
        }

        var error = await ViewModel.StartBluRayDemuxAsync();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartBluRayDemuxTitle, error);
        }
    }

    private void CancelBluRayDemuxButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelBluRayDemux();
    }

    private void ClearBluRayDemuxButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearBluRayDemuxTask();
    }

    private void SelectAllBluRayTracksButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAllBluRayTracks();
    }

    private void InvertBluRayTracksButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.InvertBluRayTrackSelection();
    }

    private void BluRayTrackListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.ToggleBluRayTrackSelection(e.ClickedItem as ViewModels.BluRayTrackItemViewModel);
    }
}
