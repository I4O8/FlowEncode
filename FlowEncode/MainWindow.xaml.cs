using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FlowEncode.Application;
using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using FlowEncode.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace FlowEncode;

public sealed partial class MainWindow : Window
{
    private const double SetupGuideScrollEdgeTolerance = 24.0;
    private const string TemplateExchangeFileExtension = ".profile";
    private const int WindowMessageSetIcon = 0x0080;
    private const int WindowIconSmall = 0;
    private const int WindowIconLarge = 1;
    private const int WindowClassLongIcon = -14;
    private const int WindowClassLongSmallIcon = -34;
    private const int DashboardCardCount = 6;
    private static readonly TimeSpan SetupGuideWheelPageTurnCooldown = TimeSpan.FromMilliseconds(280);
    private readonly AppLaunchActivation _launchActivation;
    private readonly LocalAppSettingsService _localAppSettingsService;
    private readonly SemaphoreSlim _externalVapourSynthOpenLock = new(1, 1);
    private readonly TaskCompletionSource<bool> _windowReadyCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DataPackageView? _activeDragDataView;
    private bool? _activeDragContainsSupportedScript;
    private bool _isWindowReady;
    private bool _hasCompletedInitialization;
    private bool _isPersistingSettings;
    private bool _selectionSyncInProgress;
    private bool _isCloseConfirmationInProgress;
    private bool _isShutdownConfirmed;
    private bool _closeCleanupCompleted;
    private SetupGuideScrollAnchor _pendingSetupGuideScrollAnchor;
    private SetupGuideWheelDirection _armedSetupGuideWheelDirection;
    private int _armedSetupGuideWheelCardIndex = -1;
    private DateTimeOffset _suppressSetupGuideWheelUntilUtc = DateTimeOffset.MinValue;
    private IntPtr _windowLargeIconHandle;
    private IntPtr _windowSmallIconHandle;
    private const int ShowWindowRestore = 9;

    private enum SetupGuideScrollAnchor
    {
        None,
        Top,
        Bottom
    }

    private enum SetupGuideWheelDirection
    {
        None,
        Up,
        Down
    }

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel, AppLaunchActivation launchActivation, LocalAppSettingsService localAppSettingsService)
    {
        ViewModel = viewModel;
        _launchActivation = launchActivation;
        _localAppSettingsService = localAppSettingsService;
        InitializeComponent();

        RootLayout.DataContext = ViewModel;
        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        DashboardPanel.SizeChanged += DashboardPanel_SizeChanged;
        OverviewPanel.SizeChanged += OverviewPanel_SizeChanged;
        SetupGuideFlipView.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(SetupGuideCardScrollViewer_PointerWheelChanged), true);
        SourcePathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(SourcePathTextBox_DoubleTapped), true);
        OutputPathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(OutputPathTextBox_DoubleTapped), true);
        AutoSourcePathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(AutoSourcePathTextBox_DoubleTapped), true);
        AutoOutputPathTextBox.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(AutoOutputPathTextBox_DoubleTapped), true);
        InitializeAudioProcessingInteractions();
        InitializeBluRayDemuxInteractions();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ApplyEmbeddedAppIcon();

        AppWindow.Closing += AppWindow_Closing;

        Activated += MainWindow_Activated;

        if (_launchActivation.HasRequestedVapourSynthFile)
        {
            SelectNavigationItem("vapoursynth-workspace");
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        var runningJobCount = ViewModel.Jobs.Count(job => job.State == EncodingJobState.Running);
        var isAutoCompressionRunning = ViewModel.IsAutoCompressionRunning;
        var isAudioProcessingRunning = ViewModel.IsAudioProcessingRunning;
        var isBluRayDemuxRunning = ViewModel.IsBluRayDemuxRunning;
        var hasRunningWork = runningJobCount > 0 || isAutoCompressionRunning || isAudioProcessingRunning || isBluRayDemuxRunning;

        if (_isShutdownConfirmed)
        {
            PrepareForClose();
            return;
        }

        args.Cancel = true;
        if (_isCloseConfirmationInProgress)
        {
            return;
        }

        _isCloseConfirmationInProgress = true;

        try
        {
            if (!await VapourSynthWorkspacePanel.PrepareForAppCloseAsync(RootLayout.XamlRoot))
            {
                return;
            }

            if (hasRunningWork)
            {
                var confirmed = await ShowConfirmationAsync(
                    ViewModel.Texts.CloseRunningJobsTitle,
                    ViewModel.Texts.CloseRunningWorkMessage(runningJobCount, isAutoCompressionRunning, isAudioProcessingRunning, isBluRayDemuxRunning),
                    ViewModel.Texts.CloseRunningJobsButton,
                    ViewModel.Texts.CancelButton,
                    ContentDialogButton.Close);

                if (!confirmed)
                {
                    return;
                }

                await ViewModel.CancelRunningJobsForShutdownAsync();
            }

            await VapourSynthWorkspacePanel.ClosePreviewWindowForAppShutdownAsync();
            _isShutdownConfirmed = true;
            PrepareForClose();
            Close();
        }
        finally
        {
            _isCloseConfirmationInProgress = false;
        }
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        ApplyEmbeddedAppIcon();
        _isWindowReady = true;
        UpdateAdaptiveLayout(RootLayout.ActualWidth);
        await Task.Yield();
        await ViewModel.InitializeAsync();
        await ShowRecoveredSettingsNoticeIfNeededAsync();
        ApplyTheme(ViewModel.CurrentThemePreference);
        if (_launchActivation.HasRequestedVapourSynthFile)
        {
            SelectNavigationItem("vapoursynth-workspace");
        }

        if (TemplateLibraryList.Items.Count > 0 && TemplateLibraryList.SelectedIndex < 0)
        {
            TemplateLibraryList.SelectedIndex = 0;
        }

        _hasCompletedInitialization = true;
        _windowReadyCompletionSource.TrySetResult(true);
    }

    private async void RootLayout_DragOver(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.AcceptedOperation = await ContainsSupportedScriptFileAsync(e.DataView)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void RootLayout_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var storageItems = await e.DataView.GetStorageItemsAsync();
        var file = storageItems
            .OfType<StorageFile>()
            .FirstOrDefault(static item => AppLaunchActivation.IsSupportedScriptExtension(item.Path));

        if (file is null)
        {
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Copy;
        ResetActiveDragState();
        await HandleExternalVapourSynthOpenAsync(file.Path);
    }

    private void RootLayout_DragLeave(object sender, DragEventArgs e)
    {
        ResetActiveDragState();
    }

    private void RootLayout_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyTitleBarColors(sender.ActualTheme);
        VapourSynthWorkspacePanel.UpdateEditorPresentation(sender.ActualTheme);
    }

    public async Task HandleExternalVapourSynthOpenAsync(string filePath)
    {
        var normalizedPath = AppLaunchActivation.NormalizeSupportedScriptPath(filePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        await _externalVapourSynthOpenLock.WaitAsync();

        try
        {
            await _windowReadyCompletionSource.Task;
            ActivateAndBringToFront();
            SelectNavigationItem("vapoursynth-workspace");
            await Task.Yield();
            await VapourSynthWorkspacePanel.OpenExternalDocumentAsync(normalizedPath);
            ActivateAndBringToFront();
        }
        finally
        {
            _externalVapourSynthOpenLock.Release();
        }
    }

    private async void ShellNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItemContainer?.Tag?.ToString()
            ?? (ShellNavigationView.SelectedItem as NavigationViewItem)?.Tag?.ToString()
            ?? "dashboard";
        DashboardPanel.Visibility = tag == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        VapourSynthWorkspacePanel.Visibility = tag == "vapoursynth-workspace" ? Visibility.Visible : Visibility.Collapsed;
        OverviewPanel.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        TemplatesPanel.Visibility = tag == "templates" ? Visibility.Visible : Visibility.Collapsed;
        AutoCompressionPanel.Visibility = tag == "auto-compress" ? Visibility.Visible : Visibility.Collapsed;
        AudioProcessingPanel.Visibility = tag == "audio-process" ? Visibility.Visible : Visibility.Collapsed;
        BluRayDemuxPanel.Visibility = tag == "bluray-demux" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "settings" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "settings")
        {
            await ViewModel.EnsureSetupGuideCardsAsync();
        }

        UpdateAdaptiveLayout(RootLayout.ActualWidth);
    }

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width);
    }

    private void DashboardPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isWindowReady)
        {
            return;
        }

        UpdateAdaptiveLayout(RootLayout.ActualWidth);
    }

    private void OverviewPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isWindowReady)
        {
            return;
        }

        ScheduleOverviewWorkspaceHeightRefresh(RootLayout.ActualWidth < 1320);
    }

    private void UpdateAdaptiveLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var stackedWorkspace = width < 1000;
        var compactForms = width < 700;
        var contentPadding = width < 1100
            ? new Thickness(18, 12, 18, 20)
            : width < 1400
                ? new Thickness(22, 14, 22, 24)
                : new Thickness(28, 16, 28, 28);

        DashboardContentStack.Padding = contentPadding;
        OverviewContentStack.Padding = contentPadding;
        TemplatesContentStack.Padding = contentPadding;
        AutoCompressionContentStack.Padding = contentPadding;
        AudioProcessingContentStack.Padding = contentPadding;
        BluRayDemuxContentStack.Padding = contentPadding;
        SettingsContentStack.Padding = contentPadding;
        DashboardContentStack.Spacing = width < 1100 ? 18 : 24;
        DashboardHeroCard.Padding = width < 1100 ? new Thickness(20) : new Thickness(24);
        DashboardHeroCard.MinHeight = width < 1100 ? 132 : 148;
        DashboardHeaderTitle.FontSize = width < 1100 ? 34 : 38;
        DashboardHeaderPanel.MaxWidth = width < 1100 ? 720 : 960;
        DashboardHeaderPanel.Margin = new Thickness(GetDashboardHeroTextOffset(width), 0, 0, 0);
        DashboardHeroGrid.ColumnSpacing = width < 1100 ? 16 : 20;
        var dashboardHeroIconSize = width < 1100 ? 108 : 136;
        DashboardHeroIconFrame.Width = dashboardHeroIconSize;
        DashboardHeroIconFrame.Height = dashboardHeroIconSize;
        DashboardHeroIconFrame.Visibility = width < 760 ? Visibility.Collapsed : Visibility.Visible;

        ApplyDashboardLayout(width);
        ApplyOverviewLayout(stackedWorkspace, compactForms, width);
        ApplyTemplateLayout(stackedWorkspace, compactForms);
        ApplyAutoCompressionLayout(compactForms, width);
        ApplyAudioProcessingLayout(stackedWorkspace, compactForms);
        ApplyBluRayDemuxLayout(stackedWorkspace, compactForms);
        ApplySettingsLayout(width < 700);
        UpdateSetupGuideLayout(width);
    }

    private static double GetDashboardHeroTextOffset(double width)
    {
        if (width < 1100)
        {
            return 0;
        }

        return Math.Clamp((width - 1100) * 0.12, 0, 120);
    }

    private void ApplyDashboardLayout(double width)
    {
        var columnCount = width >= 1180
            ? 3
            : width >= 700
                ? 2
                : 1;
        var rowCount = (int)Math.Ceiling((double)DashboardCardCount / columnCount);
        var rowSpacing = width < 1100 ? 16 : 20;

        DashboardCardGrid.ColumnSpacing = columnCount == 1 ? 0 : rowSpacing;
        DashboardCardGrid.RowSpacing = rowSpacing;
        DashboardPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        DashboardSecondaryColumn.Width = columnCount >= 2
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        DashboardTertiaryColumn.Width = columnCount >= 3
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);

        var cardHeight = width < 1100 ? 216 : 248;
        DashboardDemuxButton.MinHeight = cardHeight;
        DashboardVapourSynthButton.MinHeight = cardHeight;
        DashboardVideoEncodeButton.MinHeight = cardHeight;
        DashboardAudioButton.MinHeight = cardHeight;
        DashboardAutoCompressionButton.MinHeight = cardHeight;
        DashboardSettingsButton.MinHeight = cardHeight;

        var stretchedCardGridHeight = ResolveDashboardCardGridHeight(rowCount, rowSpacing, cardHeight, columnCount, width);
        DashboardCardGrid.Height = stretchedCardGridHeight ?? double.NaN;
        ConfigureDashboardRows(rowCount, stretchedCardGridHeight.HasValue);

        ArrangeDashboardCard(DashboardDemuxButton, 0, columnCount);
        ArrangeDashboardCard(DashboardVapourSynthButton, 1, columnCount);
        ArrangeDashboardCard(DashboardVideoEncodeButton, 2, columnCount);
        ArrangeDashboardCard(DashboardAudioButton, 3, columnCount);
        ArrangeDashboardCard(DashboardAutoCompressionButton, 4, columnCount);
        ArrangeDashboardCard(DashboardSettingsButton, 5, columnCount);
    }

    private void ApplyOverviewLayout(bool stackedWorkspace, bool compactForms, double width)
    {
        OverviewWorkspaceGrid.ColumnSpacing = stackedWorkspace ? 0 : 20;
        OverviewWorkspaceGrid.RowSpacing = stackedWorkspace ? 20 : 0;
        OverviewPrimaryColumn.Width = new GridLength(stackedWorkspace ? 1 : 0.85, GridUnitType.Star);
        OverviewSecondaryColumn.Width = stackedWorkspace
            ? new GridLength(0)
            : new GridLength(1.15, GridUnitType.Star);
        OverviewWorkspacePrimaryRow.Height = GridLength.Auto;
        OverviewWorkspaceSecondaryRow.Height = stackedWorkspace ? GridLength.Auto : new GridLength(0);

        Grid.SetRow(OverviewComposerPanel, 0);
        Grid.SetColumn(OverviewComposerPanel, 0);
        Grid.SetColumnSpan(OverviewComposerPanel, 1);
        Grid.SetRow(OverviewQueuePanel, stackedWorkspace ? 1 : 0);
        Grid.SetColumn(OverviewQueuePanel, stackedWorkspace ? 0 : 1);
        Grid.SetColumnSpan(OverviewQueuePanel, stackedWorkspace ? 2 : 1);

        if (stackedWorkspace)
        {
            ClearOverviewWorkspaceHeight();
        }
        else
        {
            OverviewComposerPanel.Height = double.NaN;
            OverviewQueuePanel.Height = double.NaN;
            ScheduleOverviewWorkspaceHeightRefresh(stackedWorkspace);
        }

        ConfigureTwoItemGrid(SourcePathGrid, SourcePathActionColumn, SourcePathBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(OutputPathGrid, OutputPathActionColumn, OutputPathBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(QueueActionGrid, QueueActionSecondaryColumn, QueueAndStartButton, compactForms, new GridLength(1, GridUnitType.Star));
        ConfigureThreeItemGrid(DraftEncoderRatePresetGrid, DraftRateColumn, DraftPresetColumn, DraftRateControlComboBox, DraftPresetComboBox, compactForms);
        ConfigureThreeItemGrid(DraftTuneProfileFormatValueGrid, DraftProfileColumn, DraftOutputFormatColumn, DraftProfileComboBox, DraftOutputFormatComboBox, compactForms);
        ConfigureTwoItemGrid(DraftRateValueGrid, DraftRateValueInputColumn, DraftRateValueEditorHost, compactForms || width < 1240, new GridLength(220));
        ConfigureTwoItemGrid(OverviewTemplateActionGrid, OverviewTemplateActionSecondaryColumn, SaveCurrentConfigurationButton, compactForms, GridLength.Auto);
    }

    private static void ArrangeDashboardCard(FrameworkElement card, int index, int columnCount)
    {
        Grid.SetRow(card, index / columnCount);
        Grid.SetColumn(card, index % columnCount);
    }

    private double? ResolveDashboardCardGridHeight(
        int rowCount,
        double rowSpacing,
        double cardMinHeight,
        int columnCount,
        double width)
    {
        if (columnCount == 1 || DashboardPanel.ActualHeight <= 0 || DashboardHeroCard.ActualHeight <= 0)
        {
            return null;
        }

        var verticalChrome = DashboardContentStack.Padding.Top
            + DashboardContentStack.Padding.Bottom
            + DashboardHeroCard.ActualHeight
            + DashboardContentStack.Spacing;
        var availableHeight = DashboardPanel.ActualHeight - verticalChrome;
        var minimumGridHeight = (rowCount * cardMinHeight) + ((rowCount - 1) * rowSpacing);
        if (availableHeight <= minimumGridHeight + 24)
        {
            return null;
        }

        var maxRowHeight = columnCount >= 3
            ? width >= 1600 ? 328.0 : 304.0
            : 276.0;
        var maximumGridHeight = (rowCount * maxRowHeight) + ((rowCount - 1) * rowSpacing);

        return Math.Min(availableHeight, maximumGridHeight);
    }

    private void ConfigureDashboardRows(int visibleRows, bool stretch)
    {
        var rowHeights = new[]
        {
            DashboardRow0,
            DashboardRow1,
            DashboardRow2,
            DashboardRow3,
            DashboardRow4,
            DashboardRow5
        };

        for (var index = 0; index < rowHeights.Length; index++)
        {
            rowHeights[index].Height = index < visibleRows
                ? stretch
                    ? new GridLength(1, GridUnitType.Star)
                    : GridLength.Auto
                : new GridLength(0);
        }
    }

    private void ApplyTemplateLayout(bool stackedWorkspace, bool compactForms)
    {
        TemplatesWorkspaceGrid.ColumnSpacing = stackedWorkspace ? 0 : 20;
        TemplatesPrimaryColumn.Width = new GridLength(stackedWorkspace ? 1 : 0.94, GridUnitType.Star);
        TemplatesSecondaryColumn.Width = stackedWorkspace
            ? new GridLength(0)
            : new GridLength(1.06, GridUnitType.Star);

        Grid.SetRow(TemplateEditorPanel, stackedWorkspace ? 1 : 0);
        Grid.SetColumn(TemplateEditorPanel, stackedWorkspace ? 0 : 1);
        Grid.SetColumnSpan(TemplateEditorPanel, stackedWorkspace ? 2 : 1);

        ConfigureTwoItemGrid(TemplateEditorHeaderGrid, TemplateEditorActionsColumn, TemplateEditorCommandBar, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(TemplateEncoderFormatGrid, TemplateOutputColumn, TemplateOutputComboBox, compactForms, new GridLength(1, GridUnitType.Star));
        ConfigureThreeItemGrid(TemplateRatePresetTuneGrid, TemplatePresetColumn, TemplateTuneColumn, TemplatePresetComboBox, TemplateTuneComboBox, compactForms);
        ConfigureTwoItemGrid(TemplateProfileQualityGrid, TemplateQualityGroupColumn, TemplateQualityBitrateGrid, compactForms, new GridLength(1, GridUnitType.Star));
        ConfigureTwoItemGrid(TemplateQualityBitrateGrid, TemplateBitrateColumn, TemplateBitrateContainer, compactForms, new GridLength(1, GridUnitType.Star));
    }

    private void ApplyAutoCompressionLayout(bool compactForms, double width)
    {
        ConfigureTwoItemGrid(AutoSourcePathGrid, AutoSourcePathActionColumn, AutoSourceBrowseButton, compactForms, GridLength.Auto);
        ConfigureTwoItemGrid(AutoOutputPathGrid, AutoOutputPathActionColumn, AutoOutputBrowseButton, compactForms, GridLength.Auto);
        var autoOptionsColumnCount = width >= 900
            ? 4
            : width >= 640
                ? 2
                : 1;
        ConfigureFourItemGrid(
            AutoCompressionOptionsGrid,
            AutoCompressionTargetColumn,
            AutoCompressionProbesColumn,
            AutoCompressionWorkersColumn,
            AutoCompressionTargetVmafBox,
            AutoCompressionProbesBox,
            AutoCompressionWorkersBox,
            autoOptionsColumnCount);
        ConfigureTwoItemGrid(AutoCompressionActionGrid, AutoCompressionCancelColumn, CancelAutoCompressionButton, compactForms, new GridLength(1, GridUnitType.Star));
    }

    private void ApplySettingsLayout(bool compactLayout)
    {
        SettingsOverviewGrid.ColumnSpacing = compactLayout ? 0 : 24;
        SettingsControlsGrid.ColumnSpacing = compactLayout ? 0 : 18;
        SettingsControlsGrid.RowSpacing = 14;

        SettingsOverviewSecondaryColumn.Width = compactLayout
            ? new GridLength(0)
            : GridLength.Auto;
        Grid.SetRow(SettingsActionPanel, compactLayout ? 1 : 0);
        Grid.SetColumn(SettingsActionPanel, compactLayout ? 0 : 1);
        Grid.SetColumnSpan(SettingsActionPanel, compactLayout ? 2 : 1);
        SettingsActionPanel.HorizontalAlignment = compactLayout ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        SettingsActionPanel.Margin = compactLayout ? new Thickness(0, 2, 0, 0) : new Thickness(0);

        SettingsControlsSecondaryColumn.Width = compactLayout
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(SettingsControlsGrid, compactLayout ? 2 : 1);
        Grid.SetColumn(SettingsControlsGrid, 0);
        Grid.SetColumnSpan(SettingsControlsGrid, 2);

        Grid.SetColumn(SettingsSelectorsPanel, 0);
        Grid.SetRow(SettingsSelectorsPanel, 0);
        Grid.SetRowSpan(SettingsSelectorsPanel, 1);

        Grid.SetColumn(SettingsTogglePanel, compactLayout ? 0 : 1);
        Grid.SetRow(SettingsTogglePanel, compactLayout ? 1 : 0);
        Grid.SetRowSpan(SettingsTogglePanel, 1);
    }

    private static void ConfigureTwoItemGrid(
        Grid grid,
        ColumnDefinition secondColumn,
        FrameworkElement secondItem,
        bool stacked,
        GridLength expandedSecondColumnWidth)
    {
        grid.ColumnSpacing = stacked ? 0 : 12;
        secondColumn.Width = stacked ? new GridLength(0) : expandedSecondColumnWidth;
        Grid.SetRow(secondItem, stacked ? 1 : 0);
        Grid.SetColumn(secondItem, stacked ? 0 : 1);
    }

    private static void ConfigureThreeItemGrid(
        Grid grid,
        ColumnDefinition secondColumn,
        ColumnDefinition thirdColumn,
        FrameworkElement secondItem,
        FrameworkElement thirdItem,
        bool stacked)
    {
        grid.ColumnSpacing = stacked ? 0 : 12;
        secondColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        thirdColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(secondItem, stacked ? 1 : 0);
        Grid.SetColumn(secondItem, stacked ? 0 : 1);
        Grid.SetRow(thirdItem, stacked ? 2 : 0);
        Grid.SetColumn(thirdItem, stacked ? 0 : 2);
    }

    private static void ConfigureFourItemGrid(
        Grid grid,
        ColumnDefinition secondColumn,
        ColumnDefinition thirdColumn,
        ColumnDefinition fourthColumn,
        FrameworkElement secondItem,
        FrameworkElement thirdItem,
        FrameworkElement fourthItem,
        int columnCount)
    {
        grid.ColumnSpacing = columnCount == 1 ? 0 : 12;
        secondColumn.Width = columnCount >= 2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        thirdColumn.Width = columnCount >= 4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        fourthColumn.Width = columnCount >= 4 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(secondItem, columnCount == 1 ? 1 : 0);
        Grid.SetColumn(secondItem, columnCount == 1 ? 0 : 1);

        Grid.SetRow(thirdItem, columnCount >= 4 ? 0 : columnCount == 2 ? 1 : 2);
        Grid.SetColumn(thirdItem, columnCount >= 4 ? 2 : 0);

        Grid.SetRow(fourthItem, columnCount >= 4 ? 0 : columnCount == 2 ? 1 : 3);
        Grid.SetColumn(fourthItem, columnCount >= 4 ? 3 : columnCount == 2 ? 1 : 0);
    }

    private void ScheduleOverviewWorkspaceHeightRefresh(bool stackedWorkspace)
    {
        DispatcherQueue.TryEnqueue(() => UpdateOverviewWorkspaceHeight(stackedWorkspace));
    }

    private void UpdateOverviewWorkspaceHeight(bool stackedWorkspace)
    {
        if (stackedWorkspace || OverviewPanel.Visibility != Visibility.Visible)
        {
            ClearOverviewWorkspaceHeight();
            return;
        }

        if (OverviewPanel.ActualHeight <= 0)
        {
            return;
        }

        var availableHeight = OverviewPanel.ActualHeight
            - OverviewContentStack.Padding.Top
            - OverviewContentStack.Padding.Bottom;

        if (availableHeight <= 0)
        {
            return;
        }

        var naturalPanelHeight = Math.Max(OverviewComposerPanel.ActualHeight, OverviewQueuePanel.ActualHeight);
        var targetHeight = Math.Max(Math.Ceiling(availableHeight), Math.Ceiling(naturalPanelHeight));
        OverviewComposerPanel.Height = targetHeight;
        OverviewQueuePanel.Height = targetHeight;
    }

    private void ClearOverviewWorkspaceHeight()
    {
        OverviewComposerPanel.Height = double.NaN;
        OverviewQueuePanel.Height = double.NaN;
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsAppUpdateAvailable)
        {
            if (!ViewModel.CanDownloadAppUpdateInstaller)
            {
                OpenUrl(ViewModel.AppUpdateReleaseUrl);
                return;
            }

            var installerPath = await ViewModel.DownloadLatestAppInstallerAsync();
            if (string.IsNullOrWhiteSpace(installerPath))
            {
                if (ViewModel.HasAppUpdateError)
                {
                    await ShowMessageAsync(ViewModel.Texts.AppUpdateSectionTitle, ViewModel.AppUpdateStatusText);
                }

                return;
            }

            var installNow = await ShowConfirmationAsync(
                ViewModel.Texts.AppUpdateReadyTitle,
                ViewModel.Texts.AppUpdateReadyMessage,
                ViewModel.Texts.InstallNowButton,
                ViewModel.Texts.LaterButton);

            if (!installNow)
            {
                return;
            }

            if (ViewModel.HasRunningJobs
                || ViewModel.IsAutoCompressionRunning
                || ViewModel.IsAudioProcessingRunning
                || ViewModel.IsBluRayDemuxRunning)
            {
                await ShowMessageAsync(ViewModel.Texts.AppUpdateReadyTitle, ViewModel.Texts.AppUpdateInstallRequiresIdleMessage);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(installerPath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(installerPath)
                });
                Close();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(ViewModel.Texts.ErrorInstallFailedTitle, ex.Message);
            }

            return;
        }

        var result = await ViewModel.RefreshAvailableUpdatesAsync();
        if (result is null)
        {
            if (ViewModel.HasAppUpdateError)
            {
                await ShowMessageAsync(ViewModel.Texts.AppUpdateSectionTitle, ViewModel.AppUpdateStatusText);
            }

            return;
        }

        if (!result.UpdateAvailable)
        {
            await ShowMessageAsync(ViewModel.Texts.AppUpdateSectionTitle, ViewModel.AppUpdateStatusText);
        }
    }

    private void DashboardCardButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag })
        {
            return;
        }

        SelectNavigationItem(tag);
    }

    private void SelectNavigationItem(string tag)
    {
        var navigationItem = FindNavigationItem(tag);
        if (navigationItem is null)
        {
            return;
        }

        ShellNavigationView.SelectedItem = navigationItem;
    }

    private NavigationViewItem? FindNavigationItem(string tag)
    {
        return ShellNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.Ordinal));
    }

    private void OpenToolsetFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OpenPath(ViewModel.AppRootPath);
    }

    private async void OpenSetupGuideButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.OpenSetupGuideAsync();
        await Task.Yield();
        UpdateSetupGuideLayout(RootLayout.ActualWidth);
    }

    private async void RefreshSetupGuideButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshSetupGuideAsync();
    }

    private async void CheckSetupDependencyUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckSetupDependencyUpdatesAsync(ViewModel.IsSetupGuideOpen);
    }

    private async void CloseSetupGuideButton_Click(object sender, RoutedEventArgs e)
    {
        var error = ViewModel.DismissSetupGuide();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
            return;
        }

        SelectNavigationItem("dashboard");
    }

    private void SetupGuidePreviousButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSetupGuidePrevious();
    }

    private async void SetupGuideNextButton_Click(object sender, RoutedEventArgs e)
    {
        var error = ViewModel.AdvanceOrDismissSetupGuide();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
            return;
        }

        if (!ViewModel.IsSetupGuideOpen)
        {
            SelectNavigationItem("dashboard");
        }
    }

    private void SetupGuideCardScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.IsSetupGuideOpen || ViewModel.SetupGuideCards.Count == 0)
        {
            return;
        }

        var scrollViewer = ResolveSetupGuideScrollViewer(sender as DependencyObject, e.OriginalSource as DependencyObject);
        if (scrollViewer is null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow < _suppressSetupGuideWheelUntilUtc)
        {
            e.Handled = true;
            return;
        }

        var delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        var currentCardIndex = ViewModel.SelectedSetupGuideCardIndex;
        var wheelDirection = delta < 0
            ? SetupGuideWheelDirection.Down
            : SetupGuideWheelDirection.Up;
        var isAtEdge = wheelDirection == SetupGuideWheelDirection.Down
            ? IsScrollViewerAtBottom(scrollViewer)
            : IsScrollViewerAtTop(scrollViewer);

        if (!isAtEdge)
        {
            ResetSetupGuideWheelArmState();
            return;
        }

        var isArmedForCurrentPage = _armedSetupGuideWheelDirection == wheelDirection
            && _armedSetupGuideWheelCardIndex == currentCardIndex;

        if (!isArmedForCurrentPage)
        {
            ArmSetupGuideWheel(wheelDirection, currentCardIndex);
            return;
        }

        if (wheelDirection == SetupGuideWheelDirection.Down && ViewModel.CanMoveSetupGuideNext)
        {
            _pendingSetupGuideScrollAnchor = SetupGuideScrollAnchor.Top;
            ResetSetupGuideWheelArmState();
            _suppressSetupGuideWheelUntilUtc = DateTimeOffset.UtcNow + SetupGuideWheelPageTurnCooldown;
            ViewModel.MoveSetupGuideNext();
            e.Handled = true;
            return;
        }

        if (wheelDirection == SetupGuideWheelDirection.Up && ViewModel.CanMoveSetupGuidePrevious)
        {
            _pendingSetupGuideScrollAnchor = SetupGuideScrollAnchor.Bottom;
            ResetSetupGuideWheelArmState();
            _suppressSetupGuideWheelUntilUtc = DateTimeOffset.UtcNow + SetupGuideWheelPageTurnCooldown;
            ViewModel.MoveSetupGuidePrevious();
            e.Handled = true;
        }
    }

    private async void SetupGuideFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ResetSetupGuideWheelArmState();

        if (_pendingSetupGuideScrollAnchor == SetupGuideScrollAnchor.None || SetupGuideFlipView.SelectedIndex < 0)
        {
            return;
        }

        var targetAnchor = _pendingSetupGuideScrollAnchor;
        _pendingSetupGuideScrollAnchor = SetupGuideScrollAnchor.None;

        await Task.Yield();
        SetupGuideFlipView.UpdateLayout();

        if (SetupGuideFlipView.ContainerFromIndex(SetupGuideFlipView.SelectedIndex) is not DependencyObject container)
        {
            return;
        }

        var scrollViewer = FindDescendant<ScrollViewer>(container);
        if (scrollViewer is null)
        {
            return;
        }

        var verticalOffset = targetAnchor == SetupGuideScrollAnchor.Bottom
            ? scrollViewer.ScrollableHeight
            : 0;
        scrollViewer.ChangeView(null, verticalOffset, null, true);
    }

    private async void InstallSetupDependencyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SetupDependencyKind kind })
        {
            return;
        }

        string? error;
        if (ViewModel.RequiresSetupDependencyManualImport(kind))
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            InitializeWithWindow.Initialize(picker, GetMainWindowHandle());

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            error = await ViewModel.ImportSetupDependencyBinaryAsync(kind, file.Path);
        }
        else
        {
            if (ViewModel.HasManualPinnedSetupDependency(kind))
            {
                var dependencyLabel = ViewModel.GetSetupDependencyDisplayName(kind);
                var confirmed = await ShowConfirmationAsync(
                    ViewModel.Texts.ManualToolUpdateOverrideTitle,
                    ViewModel.Texts.ManualToolUpdateOverrideMessage(dependencyLabel),
                    ViewModel.Texts.UpdateButton,
                    ViewModel.Texts.CancelButton,
                    ContentDialogButton.Close);
                if (!confirmed)
                {
                    return;
                }

                error = await ViewModel.ClearManualPinnedSetupDependencyAsync(kind, refreshAfterClear: false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    await ShowMessageAsync(ViewModel.Texts.ErrorInstallFailedTitle, error);
                    return;
                }
            }

            error = await ViewModel.InstallSetupDependencyAsync(kind);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorInstallFailedTitle, error);
        }
    }

    private async void ManualSelectSetupDependencyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SetupDependencyKind kind })
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        InitializeWithWindow.Initialize(picker, GetMainWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var error = await ViewModel.PinSetupDependencyBinaryAsync(kind, file.Path);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
        }
    }

    private async void ClearManualPinnedSetupDependencyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SetupDependencyKind kind })
        {
            return;
        }

        var error = await ViewModel.ClearManualPinnedSetupDependencyAsync(kind);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
        }
    }

    private async void UninstallSetupDependencyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SetupDependencyKind kind })
        {
            return;
        }

        var error = await ViewModel.UninstallSetupDependencyAsync(kind);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorUninstallFailedTitle, error);
        }
    }

    private void OpenTaggedFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path } && !string.IsNullOrWhiteSpace(path))
        {
            OpenPath(path);
        }
    }

    private async void BrowseWorkspaceFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is null)
        {
            return;
        }

        var error = await ViewModel.PrepareWorkspaceRootChangeAsync(folderPath);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
            return;
        }

        await PersistSettingsAsync(refreshTemplateLibrary: false);
    }

    private async void BrowseSourceButton_Click(object sender, RoutedEventArgs e)
    {
        await PickSourceFileAsync();
    }

    private async void SourcePathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickSourceFileAsync();
    }

    private async Task PickSourceFileAsync()
    {
        var filePath = PickFilteredFilePath(
            ViewModel.Texts.SourceHeader,
            ViewModel.SourcePath,
            ViewModel.Texts.SupportedSourceFileTypeDescription(InputSourceSupport.PreferredPickerPattern),
            InputSourceSupport.PreferredPickerPattern);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await ApplyPickedPathAsync(SourcePathTextBox, filePath, path => ViewModel.SourcePath = path);
        }
    }

    private async void BrowseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await PickOutputFolderAsync();
    }

    private async void OutputPathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickOutputFolderAsync();
    }

    private async Task PickOutputFolderAsync()
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is not null)
        {
            await ApplyPickedPathAsync(OutputPathTextBox, folderPath, path => ViewModel.OutputPath = path);
        }
    }

    private async void BrowseAutoSourceButton_Click(object sender, RoutedEventArgs e)
    {
        await PickAutoSourceFileAsync();
    }

    private async void AutoSourcePathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickAutoSourceFileAsync();
    }

    private async Task PickAutoSourceFileAsync()
    {
        var filePath = PickFilteredFilePath(
            ViewModel.Texts.SourceHeader,
            ViewModel.AutoCompressionSourcePath,
            ViewModel.Texts.SupportedSourceFileTypeDescription(InputSourceSupport.PreferredPickerPattern),
            InputSourceSupport.PreferredPickerPattern);

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await ApplyPickedPathAsync(AutoSourcePathTextBox, filePath, path => ViewModel.AutoCompressionSourcePath = path);
        }
    }

    private async void BrowseAutoOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await PickAutoOutputFolderAsync();
    }

    private async void AutoOutputPathTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        await PickAutoOutputFolderAsync();
    }

    private async Task PickAutoOutputFolderAsync()
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is not null)
        {
            await ApplyPickedPathAsync(AutoOutputPathTextBox, folderPath, path => ViewModel.AutoCompressionOutputPath = path);
        }
    }

    private static async Task ApplyPickedPathAsync(TextBox textBox, string path, Action<string> applyPath)
    {
        textBox.Text = path;
        await Task.Yield();
        applyPath(path);
    }

    private async Task<string?> PickFolderPathAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetMainWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private string? PickFilteredFilePath(
        string dialogTitle,
        string currentPath,
        string primaryFilterLabel,
        string primaryFilterPattern)
    {
        var initialDirectory = ResolveInitialFileDialogDirectory(currentPath);
        return NativeFileDialogHelper.ShowOpenFileDialog(
            GetMainWindowHandle(),
            dialogTitle,
            initialDirectory,
            new NativeFileDialogHelper.FileDialogFilter(primaryFilterLabel, primaryFilterPattern),
            new NativeFileDialogHelper.FileDialogFilter(ViewModel.Texts.AllFilesTypeDescription, "*.*"));
    }

    private static string ResolveInitialFileDialogDirectory(string? currentPath)
    {
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            try
            {
                if (Directory.Exists(currentPath))
                {
                    return currentPath;
                }

                var directory = Path.GetDirectoryName(currentPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    return directory;
                }
            }
            catch (ArgumentException ex)
            {
                TryWriteWindowDiagnostic($"Invalid file dialog path '{currentPath}'. {ex.GetType().Name}: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                TryWriteWindowDiagnostic($"Unsupported file dialog path '{currentPath}'. {ex.GetType().Name}: {ex.Message}");
            }
            catch (PathTooLongException ex)
            {
                TryWriteWindowDiagnostic($"Overlong file dialog path '{currentPath}'. {ex.GetType().Name}: {ex.Message}");
            }
        }

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return string.IsNullOrWhiteSpace(documentsPath) ? Environment.CurrentDirectory : documentsPath;
    }

    private nint GetMainWindowHandle()
    {
        return WindowNative.GetWindowHandle(this);
    }

    private async void QueueOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        await QueueCurrentJobWithConfirmationAsync(startImmediately: false);
    }

    private async void QueueJobButton_Click(object sender, RoutedEventArgs e)
    {
        await QueueCurrentJobWithConfirmationAsync(startImmediately: true);
    }

    private async void StartAutoCompressionButton_Click(object sender, RoutedEventArgs e)
    {
        var validationError = ViewModel.ValidateAutoCompressionForStart(out var existingOutputPath);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartAutoCompressionTitle, validationError);
            return;
        }

        if (!string.IsNullOrWhiteSpace(existingOutputPath))
        {
            var overwriteConfirmed = await ShowConfirmationAsync(
                ViewModel.Texts.OverwriteOutputTitle,
                ViewModel.Texts.OverwriteOutputMessage(existingOutputPath),
                ViewModel.Texts.OverwriteButton,
                ViewModel.Texts.CancelButton,
                ContentDialogButton.Close);

            if (!overwriteConfirmed)
            {
                return;
            }
        }

        var error = await ViewModel.StartAutoCompressionAsync();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartAutoCompressionTitle, error);
        }
    }

    private void CancelAutoCompressionButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelAutoCompression();
    }

    private async void LoadTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TemplateLibraryItems.Count == 0)
        {
            await ShowMessageAsync(ViewModel.Texts.LoadTemplateDialogTitle, ViewModel.Texts.NoTemplateAvailableMessage);
            return;
        }

        var templatePicker = new ComboBox
        {
            Header = ViewModel.Texts.LoadTemplateDialogHeader,
            DisplayMemberPath = nameof(TemplateLibraryItemViewModel.Name),
            ItemsSource = ViewModel.TemplateLibraryItems.ToList(),
            SelectedItem = OverviewTemplatePicker.SelectedItem as TemplateLibraryItemViewModel
                ?? ViewModel.TemplateLibraryItems.FirstOrDefault()
        };

        var summaryTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.WrapWholeWords
        };

        void RefreshTemplateSummary()
        {
            if (templatePicker.SelectedItem is not TemplateLibraryItemViewModel item)
            {
                summaryTextBlock.Text = string.Empty;
                return;
            }

            summaryTextBlock.Text = $"{item.SourceLabel} · {item.EncoderAndQualityText}";
        }

        templatePicker.SelectionChanged += (_, _) => RefreshTemplateSummary();
        RefreshTemplateSummary();

        var dialog = new ContentDialog
        {
            Title = ViewModel.Texts.LoadTemplateDialogTitle,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    templatePicker,
                    summaryTextBlock
                }
            },
            PrimaryButtonText = ViewModel.Texts.LoadTemplateButton,
            CloseButtonText = ViewModel.Texts.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary
            || templatePicker.SelectedItem is not TemplateLibraryItemViewModel selectedTemplate)
        {
            return;
        }

        if (selectedTemplate.UserTemplate is not null)
        {
            RunWithTemplateSelectionSync(() =>
            {
                OverviewTemplatePicker.SelectedItem = ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                    string.Equals(item.Key, selectedTemplate.Key, StringComparison.Ordinal));
                SavedTemplatesQuickSelect.SelectedItem = selectedTemplate.UserTemplate;
            });

            await ViewModel.ApplyUserTemplateToEncodingDraftAsync(selectedTemplate.UserTemplate);
        }
    }

    private async void OverviewTemplatePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionSyncInProgress)
        {
            return;
        }

        if (OverviewTemplatePicker.SelectedItem is not TemplateLibraryItemViewModel templateItem)
        {
            return;
        }

        if (templateItem.UserTemplate is not null)
        {
            RunWithTemplateSelectionSync(() =>
            {
                SavedTemplatesQuickSelect.SelectedItem = templateItem.UserTemplate;
            });

            await ViewModel.ApplyUserTemplateToEncodingDraftAsync(templateItem.UserTemplate);
        }
    }

    private async void SaveCurrentConfigurationButton_Click(object sender, RoutedEventArgs e)
    {
        var nameTextBox = new TextBox
        {
            Header = ViewModel.Texts.TemplateNameHeader,
            Text = ViewModel.DraftTemplateName ?? string.Empty
        };

        var notesTextBox = new TextBox
        {
            Header = ViewModel.Texts.TemplateNotesHeader,
            AcceptsReturn = true,
            MinHeight = 96,
            Text = ViewModel.DraftTemplateNotes ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        };

        var dialog = new ContentDialog
        {
            Title = ViewModel.Texts.SaveCurrentConfigurationButton,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    nameTextBox,
                    notesTextBox
                }
            },
            PrimaryButtonText = ViewModel.Texts.SaveButton,
            CloseButtonText = ViewModel.Texts.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.DraftTemplateName = nameTextBox.Text;
        ViewModel.DraftTemplateNotes = notesTextBox.Text;

        try
        {
            await TrySaveCurrentTemplateAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveFailedTitle, ex.Message);
        }
    }

    private async void SavedTemplatesQuickSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionSyncInProgress)
        {
            return;
        }

        if (SavedTemplatesQuickSelect.SelectedItem is not SavedTemplate template)
        {
            return;
        }

        var templateItem = ViewModel.TemplateLibraryItems
            .FirstOrDefault(item => string.Equals(item.TemplateId, template.Id, StringComparison.OrdinalIgnoreCase));
        RunWithTemplateSelectionSync(() =>
        {
            TemplateLibraryList.SelectedItem = templateItem;
            OverviewTemplatePicker.SelectedItem = templateItem;
        });

        await ViewModel.SelectUserTemplateAsync(template);
    }

    private async void StartJobMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.PrioritizeJob(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotReorderTitle, error);
        }
    }

    private async void StartQueuedJobMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.StartJobNow(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartTitle, error);
        }
    }

    private async void MoveJobToTopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.MoveJobToTop(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotReorderTitle, error);
        }
    }

    private async void MoveJobUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.MoveJobUp(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotReorderTitle, error);
        }
    }

    private async void MoveJobDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.MoveJobDown(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotReorderTitle, error);
        }
    }

    private async void MoveJobToBottomMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.MoveJobToBottom(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotReorderTitle, error);
        }
    }

    private async void AbortJobMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        if (!job.CanCancel)
        {
            return;
        }

        SelectQueueJobForSingleAction(job);

        var confirmed = await ShowConfirmationAsync(
            ViewModel.Texts.ConfirmCancelJobTitle,
            ViewModel.Texts.ConfirmCancelJobMessage(job.SourceFileName, job.State),
            ViewModel.Texts.ConfirmCancelJobButton,
            ViewModel.Texts.CancelButton,
            ContentDialogButton.Close);

        if (!confirmed)
        {
            return;
        }

        await ViewModel.CancelJobAsync(job);
    }

    private async void RestartJobMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = await ViewModel.RestartJobAsync(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotRestartTitle, error);
        }
    }

    private async void DeleteJobMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetJobFromMenu(sender, out var job))
        {
            return;
        }

        SelectQueueJobForSingleAction(job);
        var error = ViewModel.RemoveJob(job);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotDeleteTitle, error);
            return;
        }

        SyncListSelectionFromViewModel();
    }

    private void SelectAllQueueJobsButton_Click(object sender, RoutedEventArgs e)
    {
        JobsList.SelectAll();
        SyncSelectedQueueJobs();
    }

    private void InvertQueueSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedJobs = JobsList.SelectedItems
            .OfType<EncodingJobItemViewModel>()
            .ToHashSet();

        JobsList.SelectedItems.Clear();
        foreach (var job in ViewModel.Jobs)
        {
            if (!selectedJobs.Contains(job))
            {
                JobsList.SelectedItems.Add(job);
            }
        }

        SyncSelectedQueueJobs();
    }

    private void ClearQueueSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        JobsList.SelectedItems.Clear();
        SyncSelectedQueueJobs();
    }

    private async void StartSelectedJobsButton_Click(object sender, RoutedEventArgs e)
    {
        SyncSelectedQueueJobs();
        var error = ViewModel.StartSelectedJobsNow();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotStartTitle, error);
        }
    }

    private async void CancelSelectedJobsButton_Click(object sender, RoutedEventArgs e)
    {
        SyncSelectedQueueJobs();
        if (ViewModel.SelectedQueueJobCount == 0)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotCancelTitle, ViewModel.Texts.NoSelectedJobsError);
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            ViewModel.Texts.ConfirmCancelSelectedJobsTitle,
            ViewModel.Texts.ConfirmCancelSelectedJobsMessage(
                ViewModel.SelectedQueueJobCount,
                ViewModel.SelectedRunningJobCount,
                ViewModel.SelectedQueuedJobCount),
            ViewModel.Texts.ConfirmCancelSelectedJobsButton,
            ViewModel.Texts.CancelButton,
            ContentDialogButton.Close);

        if (!confirmed)
        {
            return;
        }

        var error = ViewModel.CancelSelectedJobs();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotCancelTitle, error);
        }
    }

    private async void DeleteSelectedJobsButton_Click(object sender, RoutedEventArgs e)
    {
        SyncSelectedQueueJobs();
        if (ViewModel.SelectedQueueJobCount == 0)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotDeleteTitle, ViewModel.Texts.NoSelectedJobsError);
            return;
        }

        var confirmed = await ShowConfirmationAsync(
            ViewModel.Texts.ConfirmDeleteSelectedJobsTitle,
            ViewModel.Texts.ConfirmDeleteSelectedJobsMessage(
                ViewModel.SelectedQueueJobCount,
                ViewModel.SelectedRemovableQueueJobCount,
                ViewModel.SelectedRunningJobCount),
            ViewModel.Texts.ConfirmDeleteSelectedJobsButton,
            ViewModel.Texts.CancelButton,
            ContentDialogButton.Close);

        if (!confirmed)
        {
            return;
        }

        var error = ViewModel.RemoveSelectedJobs();
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotDeleteTitle, error);
            return;
        }

        SyncListSelectionFromViewModel();
    }

    private void OpenUrlButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrWhiteSpace(url))
        {
            OpenUrl(url);
        }
    }

    private static bool IsScrollViewerAtTop(ScrollViewer scrollViewer)
    {
        return scrollViewer.VerticalOffset <= SetupGuideScrollEdgeTolerance;
    }

    private static bool IsScrollViewerAtBottom(ScrollViewer scrollViewer)
    {
        return scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - SetupGuideScrollEdgeTolerance;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T matchedChild)
            {
                return matchedChild;
            }

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void ArmSetupGuideWheel(SetupGuideWheelDirection direction, int cardIndex)
    {
        _armedSetupGuideWheelDirection = direction;
        _armedSetupGuideWheelCardIndex = cardIndex;
    }

    private void ResetSetupGuideWheelArmState()
    {
        _armedSetupGuideWheelDirection = SetupGuideWheelDirection.None;
        _armedSetupGuideWheelCardIndex = -1;
    }

    private ScrollViewer? ResolveSetupGuideScrollViewer(DependencyObject? sender, DependencyObject? originalSource)
    {
        if (sender is ScrollViewer directScrollViewer)
        {
            return directScrollViewer;
        }

        var sourceScrollViewer = FindAncestor<ScrollViewer>(originalSource);
        if (sourceScrollViewer is not null)
        {
            return sourceScrollViewer;
        }

        if (SetupGuideFlipView.SelectedIndex < 0)
        {
            return null;
        }

        return SetupGuideFlipView.ContainerFromIndex(SetupGuideFlipView.SelectedIndex) is DependencyObject container
            ? FindDescendant<ScrollViewer>(container)
            : null;
    }

    private void UpdateSetupGuideLayout(double width)
    {
        if (width <= 0 || RootLayout.ActualHeight <= 0)
        {
            return;
        }

        var overlayPadding = width < 960 ? 16.0 : 20.0;
        SetupGuideDialogCard.MaxHeight = Math.Max(360.0, RootLayout.ActualHeight - (overlayPadding * 2));
        SetupGuideDialogCard.Padding = width < 960 ? new Thickness(18) : new Thickness(24);

        var headerHeight = SetupGuideHeaderPanel.ActualHeight;
        var footerHeight = SetupGuideFooterPanel.ActualHeight;
        var dialogPadding = SetupGuideDialogCard.Padding.Top + SetupGuideDialogCard.Padding.Bottom;
        var chromeHeight = headerHeight + footerHeight + dialogPadding + 36.0;
        SetupGuideFlipView.MaxHeight = Math.Max(220.0, SetupGuideDialogCard.MaxHeight - chromeHeight);
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void JobsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectedQueueJobs();

        var activeJob = e.AddedItems
            .OfType<EncodingJobItemViewModel>()
            .LastOrDefault()
            ?? JobsList.SelectedItem as EncodingJobItemViewModel
            ?? JobsList.SelectedItems
                .OfType<EncodingJobItemViewModel>()
                .LastOrDefault();

        ViewModel.SelectJob(activeJob);
    }

    private async void TemplateLibraryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionSyncInProgress)
        {
            return;
        }

        if (TemplateLibraryList.SelectedItem is not TemplateLibraryItemViewModel templateItem)
        {
            return;
        }

        await SelectTemplateItemAsync(templateItem);
    }

    private async void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await TrySaveCurrentTemplateAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorSaveFailedTitle, ex.Message);
        }
    }

    private async void NewTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmTemplateChangeAsync())
        {
            RestoreCurrentTemplateSelection();
            return;
        }

        RunWithTemplateSelectionSync(() =>
        {
            TemplateLibraryList.SelectedItem = null;
            SavedTemplatesQuickSelect.SelectedItem = null;
        });

        ViewModel.BeginNewTemplateDraft();
    }

    private async void ImportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmTemplateChangeAsync())
        {
            RestoreCurrentTemplateSelection();
            return;
        }

        try
        {
            var filePath = PickTemplateImportFilePath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var importedTemplate = await ViewModel.ReadTemplateAsync(filePath);
            var existingTemplate = ViewModel.FindUserTemplateByName(importedTemplate.Name);
            if (existingTemplate?.IsPinned == true)
            {
                await ShowMessageAsync(ViewModel.Texts.ErrorImportFailedTitle, ViewModel.Texts.PinnedTemplateLockedMessage);
                RestoreCurrentTemplateSelection();
                return;
            }

            if (existingTemplate is not null)
            {
                var overwriteConfirmed = await ShowConfirmationAsync(
                    ViewModel.Texts.OverwriteTemplateTitle,
                    ViewModel.Texts.OverwriteTemplateMessage(importedTemplate.Name),
                    ViewModel.Texts.OverwriteButton,
                    ViewModel.Texts.CancelButton);

                if (!overwriteConfirmed)
                {
                    RestoreCurrentTemplateSelection();
                    return;
                }
            }

            var savedTemplate = await ViewModel.ImportTemplateAsync(importedTemplate, existingTemplate?.Id);
            var templateItem = ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                string.Equals(item.TemplateId, savedTemplate.Id, StringComparison.OrdinalIgnoreCase));
            SyncTemplateSelectors(templateItem);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorImportFailedTitle, ex.Message);
        }
    }

    private async void ExportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = PickTemplateExportFilePath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            await ViewModel.ExportCurrentTemplateAsync(filePath);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorExportFailedTitle, ex.Message);
        }
    }

    private async void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTemplateItem(sender, out var templateItem) || templateItem.UserTemplate is not { } template)
        {
            return;
        }

        try
        {
            if (template.IsPinned)
            {
                await ShowMessageAsync(ViewModel.Texts.ErrorDeleteFailedTitle, ViewModel.Texts.PinnedTemplateLockedMessage);
                return;
            }

            var confirmed = await ShowConfirmationAsync(
                ViewModel.Texts.ConfirmDeleteTemplateTitle,
                ViewModel.Texts.ConfirmDeleteTemplateMessage(template.Name),
                ViewModel.Texts.DeleteTemplateButton,
                ViewModel.Texts.CancelButton);
            if (!confirmed)
            {
                return;
            }

            var deletedCurrentTemplate = string.Equals(
                ViewModel.CurrentTemplateSelectionKey,
                templateItem.Key,
                StringComparison.Ordinal);
            await ViewModel.DeleteTemplateAsync(template.Id);

            if (deletedCurrentTemplate)
            {
                SyncTemplateSelectors(null);
            }
            else
            {
                RestoreCurrentTemplateSelection();
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorDeleteFailedTitle, ex.Message);
        }
    }

    private async void PinTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTemplateItem(sender, out var templateItem))
        {
            return;
        }

        try
        {
            var currentTemplate = templateItem.UserTemplate
                ?? ViewModel.FindUserTemplateById(templateItem.TemplateId)
                ?? throw new InvalidOperationException(ViewModel.Texts.TemplateMissingMessage);
            var updatedTemplate = await ViewModel.SetTemplatePinnedAsync(currentTemplate.Id, !currentTemplate.IsPinned);
            var updatedTemplateItem = ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                string.Equals(item.TemplateId, updatedTemplate.Id, StringComparison.OrdinalIgnoreCase));
            SyncTemplateSelectors(updatedTemplateItem);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorPinFailedTitle, ex.Message);
        }
    }

    private bool TryGetTemplateItem(object sender, out TemplateLibraryItemViewModel templateItem)
    {
        templateItem = null!;

        if (sender is Button button)
        {
            if (button.CommandParameter is TemplateLibraryItemViewModel commandItem)
            {
                templateItem = commandItem;
                return true;
            }

            if (button.DataContext is TemplateLibraryItemViewModel dataContextItem)
            {
                templateItem = dataContextItem;
                return true;
            }

            if (FindAncestor<ListViewItem>(button)?.DataContext is TemplateLibraryItemViewModel containerItem)
            {
                templateItem = containerItem;
                return true;
            }

            var templateId = button.Tag as string;
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                var matchedItem = ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                    string.Equals(item.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
                if (matchedItem is not null)
                {
                    templateItem = matchedItem;
                    return true;
                }
            }
        }

        return false;
    }

    private async void ThemeSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await PersistSettingsAsync(refreshTemplateLibrary: true);
    }

    private async void LanguageSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await PersistSettingsAsync(refreshTemplateLibrary: true);
    }

    private async void SettingsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        await PersistSettingsAsync(refreshTemplateLibrary: false);
    }

    private async void QueueHeaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: not null })
        {
            return;
        }

        await PersistSettingsAsync(refreshTemplateLibrary: false);
    }

    private void SyncSelectedQueueJobs()
    {
        ViewModel.UpdateSelectedQueueJobs(JobsList.SelectedItems.OfType<EncodingJobItemViewModel>());
    }

    private void SelectQueueJobForSingleAction(EncodingJobItemViewModel job)
    {
        if (!JobsList.SelectedItems.Contains(job))
        {
            JobsList.SelectedItems.Add(job);
        }

        SyncSelectedQueueJobs();
        ViewModel.SelectJob(job);
    }

    private void SyncListSelectionFromViewModel()
    {
        var selectedJobs = JobsList.SelectedItems
            .OfType<EncodingJobItemViewModel>()
            .Where(job => ViewModel.Jobs.Contains(job))
            .ToList();

        if (selectedJobs.Count == JobsList.SelectedItems.Count)
        {
            return;
        }

        JobsList.SelectedItems.Clear();
        foreach (var job in selectedJobs)
        {
            JobsList.SelectedItems.Add(job);
        }

        SyncSelectedQueueJobs();
    }

    private async Task<bool> ShowConfirmationAsync(
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText,
        ContentDialogButton defaultButton = ContentDialogButton.Primary)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = defaultButton,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task QueueCurrentJobWithConfirmationAsync(bool startImmediately)
    {
        var preflight = ViewModel.AnalyzeCurrentJobForQueue();
        if (!string.IsNullOrWhiteSpace(preflight.ValidationError))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotQueueTitle, preflight.ValidationError);
            return;
        }

        if (preflight.RunningOutputConflict is not null)
        {
            await ShowMessageAsync(
                ViewModel.Texts.ErrorCannotQueueTitle,
                ViewModel.Texts.QueueOutputPathRunningConflictMessage(
                    preflight.RunningOutputConflict.SourceFileName,
                    preflight.BaseOutputPath));
            return;
        }

        if (preflight.DuplicateJob is not null)
        {
            var duplicateConfirmed = await ShowConfirmationAsync(
                ViewModel.Texts.ConfirmDuplicateQueueJobTitle,
                ViewModel.Texts.ConfirmDuplicateQueueJobMessage(
                    preflight.DuplicateJob.SourceFileName,
                    preflight.BaseOutputPath,
                    preflight.FinalOutputPath),
                ViewModel.Texts.ConfirmDuplicateQueueJobButton,
                ViewModel.Texts.CancelButton,
                ContentDialogButton.Close);

            if (!duplicateConfirmed)
            {
                return;
            }
        }

        var error = await ViewModel.QueueCurrentJobAsync(startImmediately, preflight);
        if (!string.IsNullOrWhiteSpace(error))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotQueueTitle, error);
            return;
        }

        if (ViewModel.SelectedJob is not null)
        {
            JobsList.SelectedItems.Clear();
            JobsList.SelectedItems.Add(ViewModel.SelectedJob);
        }

        SyncSelectedQueueJobs();
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = ViewModel.Texts.OkButton,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme
        };

        await dialog.ShowAsync();
    }

    private async Task ShowRecoveredSettingsNoticeIfNeededAsync()
    {
        var recoveryInfo = _localAppSettingsService.ConsumeLastLoadRecoveryInfo();
        if (recoveryInfo is null)
        {
            return;
        }

        await ShowMessageAsync(
            ViewModel.Texts.SettingsRecoveredTitle,
            ViewModel.Texts.SettingsRecoveredMessage(
                recoveryInfo.BackupPath,
                recoveryInfo.LoadError,
                recoveryInfo.BackupError));
    }

    private async Task<SavedTemplate?> TrySaveCurrentTemplateAsync()
    {
        var normalizedTemplateName = ViewModel.DraftTemplateName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTemplateName))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotSaveTemplateTitle, ViewModel.Texts.EmptyTemplateNameMessage);
            return null;
        }

        var existingTemplate = ViewModel.FindUserTemplateByName(normalizedTemplateName);
        if (existingTemplate?.IsPinned == true
            && !string.Equals(existingTemplate.Id, ViewModel.EditingUserTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotSaveTemplateTitle, ViewModel.Texts.PinnedTemplateLockedMessage);
            return null;
        }

        if (existingTemplate is not null
            && !string.Equals(existingTemplate.Id, ViewModel.EditingUserTemplateId, StringComparison.OrdinalIgnoreCase))
        {
            var overwriteConfirmed = await ShowConfirmationAsync(
                ViewModel.Texts.OverwriteTemplateTitle,
                ViewModel.Texts.OverwriteTemplateMessage(normalizedTemplateName),
                ViewModel.Texts.OverwriteButton,
                ViewModel.Texts.CancelButton);

            if (!overwriteConfirmed)
            {
                return null;
            }
        }

        var savedTemplate = await ViewModel.SaveCurrentTemplateAsync();
        if (savedTemplate is null)
        {
            await ShowMessageAsync(ViewModel.Texts.ErrorCannotSaveTemplateTitle, ViewModel.Texts.IncompleteTemplateMessage);
            return null;
        }

        RunWithTemplateSelectionSync(() =>
        {
            SavedTemplatesQuickSelect.SelectedItem = savedTemplate;
            TemplateLibraryList.SelectedItem = ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                string.Equals(item.TemplateId, savedTemplate.Id, StringComparison.OrdinalIgnoreCase));
        });

        return savedTemplate;
    }

    private async Task<bool> ConfirmTemplateChangeAsync()
    {
        if (!ViewModel.HasUnsavedTemplateChanges)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            Title = ViewModel.Texts.UnsavedTemplateChangesTitle,
            Content = ViewModel.Texts.UnsavedTemplateChangesMessage,
            PrimaryButtonText = ViewModel.Texts.SaveButton,
            SecondaryButtonText = ViewModel.Texts.DontSaveButton,
            CloseButtonText = ViewModel.Texts.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => await TrySaveCurrentTemplateAsync() is not null,
            ContentDialogResult.Secondary => true,
            _ => false
        };
    }

    private void RestoreCurrentTemplateSelection()
    {
        RunWithTemplateSelectionSync(() =>
        {
            var selectedItem = string.IsNullOrWhiteSpace(ViewModel.CurrentTemplateSelectionKey)
                ? null
                : ViewModel.TemplateLibraryItems.FirstOrDefault(item =>
                    string.Equals(item.Key, ViewModel.CurrentTemplateSelectionKey, StringComparison.Ordinal));
            TemplateLibraryList.SelectedItem = selectedItem;
            OverviewTemplatePicker.SelectedItem = selectedItem;

            SavedTemplatesQuickSelect.SelectedItem = (TemplateLibraryList.SelectedItem as TemplateLibraryItemViewModel)?.UserTemplate;
        });
    }

    private async Task SelectTemplateItemAsync(TemplateLibraryItemViewModel templateItem)
    {
        if (string.Equals(templateItem.Key, ViewModel.CurrentTemplateSelectionKey, StringComparison.Ordinal))
        {
            SyncTemplateSelectors(templateItem);
            return;
        }

        if (!await ConfirmTemplateChangeAsync())
        {
            RestoreCurrentTemplateSelection();
            return;
        }

        SyncTemplateSelectors(templateItem);

        if (templateItem.UserTemplate is not null)
        {
            await ViewModel.SelectUserTemplateAsync(templateItem.UserTemplate);
        }
    }

    private void SyncTemplateSelectors(TemplateLibraryItemViewModel? templateItem)
    {
        RunWithTemplateSelectionSync(() =>
        {
            TemplateLibraryList.SelectedItem = templateItem;
            OverviewTemplatePicker.SelectedItem = templateItem;
            SavedTemplatesQuickSelect.SelectedItem = templateItem?.UserTemplate;
        });
    }

    private void RunWithTemplateSelectionSync(Action action)
    {
        _selectionSyncInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _selectionSyncInProgress = false;
        }
    }

    private string? PickTemplateImportFilePath()
    {
        var initialDirectory = EnsureTemplateFilesRootPath();
        return NativeFileDialogHelper.ShowOpenFileDialog(
            WindowNative.GetWindowHandle(this),
            ViewModel.Texts.LoadTemplateDialogTitle,
            initialDirectory,
            new NativeFileDialogHelper.FileDialogFilter(
                ViewModel.Texts.TemplateFileTypeDescription,
                $"*{TemplateExchangeFileExtension}"));
    }

    private string? PickTemplateExportFilePath()
    {
        var initialDirectory = EnsureTemplateFilesRootPath();
        return NativeFileDialogHelper.ShowSaveFileDialog(
            WindowNative.GetWindowHandle(this),
            ViewModel.Texts.ExportButton,
            initialDirectory,
            BuildTemplateExportFileName(),
            TemplateExchangeFileExtension,
            new NativeFileDialogHelper.FileDialogFilter(
                ViewModel.Texts.TemplateFileTypeDescription,
                $"*{TemplateExchangeFileExtension}"));
    }

    private string BuildTemplateExportFileName()
    {
        var preferredName = ViewModel.DraftTemplateName?.Trim();
        return SanitizeFileName(string.IsNullOrWhiteSpace(preferredName) ? "template" : preferredName);
    }

    private string EnsureTemplateFilesRootPath()
    {
        Directory.CreateDirectory(ViewModel.TemplateFilesRootPath);
        return ViewModel.TemplateFilesRootPath;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "template" : sanitized;
    }

    private static bool TryGetJobFromMenu(object sender, out EncodingJobItemViewModel job)
    {
        job = null!;

        if (sender is MenuFlyoutItem { CommandParameter: EncodingJobItemViewModel parameter })
        {
            job = parameter;
            return true;
        }

        return false;
    }

    private static void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void ActivateAndBringToFront()
    {
        Activate();

        var windowHandle = WindowNative.GetWindowHandle(this);
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        if (IsIconic(windowHandle))
        {
            ShowWindow(windowHandle, ShowWindowRestore);
        }

        SetForegroundWindow(windowHandle);
    }

    public void BringToFront()
    {
        ActivateAndBringToFront();
    }

    private void ApplyTitleBarColors(ElementTheme actualTheme)
    {
        var titleBar = AppWindow.TitleBar;
        var foregroundColor = ResolveThemeColor(actualTheme, "TitleBarButtonForegroundColor");
        var inactiveForegroundColor = ResolveThemeColor(actualTheme, "TitleBarButtonInactiveForegroundColor");

        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ForegroundColor = foregroundColor;
        titleBar.InactiveForegroundColor = inactiveForegroundColor;
        titleBar.ButtonForegroundColor = foregroundColor;
        titleBar.ButtonHoverForegroundColor = foregroundColor;
        titleBar.ButtonPressedForegroundColor = foregroundColor;
        titleBar.ButtonInactiveForegroundColor = inactiveForegroundColor;
    }

    private static Windows.UI.Color ResolveThemeColor(ElementTheme actualTheme, string resourceKey)
    {
        var themeKey = actualTheme == ElementTheme.Light ? "Light" : "Dark";

        try
        {
            if (Microsoft.UI.Xaml.Application.Current.Resources.ThemeDictionaries[themeKey] is ResourceDictionary themeDictionary)
            {
                var resource = themeDictionary[resourceKey];
                return resource switch
                {
                    Windows.UI.Color color => color,
                    SolidColorBrush brush => brush.Color,
                    _ => actualTheme == ElementTheme.Light ? Colors.Black : Colors.White
                };
            }
        }
        catch (Exception ex)
        {
            TryWriteWindowDiagnostic($"Failed to resolve theme resource '{resourceKey}' from '{themeKey}'. {ex.GetType().Name}: {ex.Message}");
        }

        return actualTheme == ElementTheme.Light ? Colors.Black : Colors.White;
    }

    private void ApplyTheme(AppThemePreference preference)
    {
        RootLayout.RequestedTheme = preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        ApplyTitleBarColors(RootLayout.ActualTheme);
    }

    private async Task<bool> ContainsSupportedScriptFileAsync(DataPackageView dataView)
    {
        if (!dataView.Contains(StandardDataFormats.StorageItems))
        {
            return false;
        }

        if (ReferenceEquals(_activeDragDataView, dataView) && _activeDragContainsSupportedScript.HasValue)
        {
            return _activeDragContainsSupportedScript.Value;
        }

        try
        {
            var storageItems = await dataView.GetStorageItemsAsync().AsTask();
            var containsSupportedScript = storageItems
                .OfType<StorageFile>()
                .Any(static item => AppLaunchActivation.IsSupportedScriptExtension(item.Path));
            _activeDragDataView = dataView;
            _activeDragContainsSupportedScript = containsSupportedScript;
            return containsSupportedScript;
        }
        catch (Exception ex)
        {
            _activeDragDataView = dataView;
            _activeDragContainsSupportedScript = false;
            TryWriteWindowDiagnostic($"Failed to inspect drag-and-drop storage items. {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void ResetActiveDragState()
    {
        _activeDragDataView = null;
        _activeDragContainsSupportedScript = null;
    }

    private async Task<bool> PersistSettingsAsync(bool refreshTemplateLibrary)
    {
        if (!_isWindowReady || !_hasCompletedInitialization || _isPersistingSettings)
        {
            return false;
        }

        _isPersistingSettings = true;

        try
        {
            var error = ViewModel.SaveSettings();
            if (!string.IsNullOrWhiteSpace(error))
            {
                await ShowMessageAsync(ViewModel.Texts.ErrorSaveSettingsFailedTitle, error);
                return false;
            }

            ApplyTheme(ViewModel.CurrentThemePreference);
            VapourSynthWorkspacePanel.ViewModel.ApplyLanguage(ViewModel.CurrentLanguagePreference);
            VapourSynthWorkspacePanel.UpdateEditorPresentation(RootLayout.ActualTheme);
            VapourSynthWorkspacePanel.UpdatePreviewPresentation(
                ViewModel.CurrentLanguagePreference,
                ViewModel.CurrentThemePreference);

            if (refreshTemplateLibrary)
            {
                ViewModel.RefreshTemplateLibraryView();
                RestoreCurrentTemplateSelection();
            }

            return true;
        }
        finally
        {
            _isPersistingSettings = false;
        }
    }

    private void ApplyEmbeddedAppIcon()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
        {
            return;
        }

        var windowHandle = WindowNative.GetWindowHandle(this);
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        var largeIcons = new[] { IntPtr.Zero };
        var smallIcons = new[] { IntPtr.Zero };
        var copiedLargeIcon = IntPtr.Zero;
        var copiedSmallIcon = IntPtr.Zero;

        try
        {
            var extractedCount = ExtractIconEx(processPath, 0, largeIcons, smallIcons, 1);
            if (extractedCount == 0 || extractedCount == uint.MaxValue)
            {
                return;
            }

            var iconHandle = smallIcons[0] != IntPtr.Zero
                ? smallIcons[0]
                : largeIcons[0];
            if (iconHandle == IntPtr.Zero)
            {
                return;
            }

            copiedSmallIcon = CopyIcon(smallIcons[0] != IntPtr.Zero ? smallIcons[0] : iconHandle);
            copiedLargeIcon = CopyIcon(largeIcons[0] != IntPtr.Zero ? largeIcons[0] : iconHandle);

            var persistentLargeIcon = copiedLargeIcon != IntPtr.Zero ? copiedLargeIcon : copiedSmallIcon;
            var persistentSmallIcon = copiedSmallIcon != IntPtr.Zero ? copiedSmallIcon : persistentLargeIcon;
            if (persistentLargeIcon == IntPtr.Zero && persistentSmallIcon == IntPtr.Zero)
            {
                return;
            }

            ReleaseWindowIcons();
            _windowLargeIconHandle = persistentLargeIcon;
            _windowSmallIconHandle = persistentSmallIcon;
            copiedLargeIcon = IntPtr.Zero;
            copiedSmallIcon = IntPtr.Zero;

            ApplyWindowIconHandles(windowHandle);
        }
        catch (Exception ex)
        {
            TryWriteWindowDiagnostic($"Failed to apply embedded app icon from '{processPath}'. {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            DestroyUniqueIconHandles(copiedSmallIcon, copiedLargeIcon, smallIcons[0], largeIcons[0]);
        }
    }

    private void ApplyWindowIconHandles(IntPtr windowHandle)
    {
        var smallIcon = _windowSmallIconHandle != IntPtr.Zero
            ? _windowSmallIconHandle
            : _windowLargeIconHandle;
        var largeIcon = _windowLargeIconHandle != IntPtr.Zero
            ? _windowLargeIconHandle
            : smallIcon;

        if (smallIcon == IntPtr.Zero && largeIcon == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var iconId = Microsoft.UI.Win32Interop.GetIconIdFromIcon(smallIcon != IntPtr.Zero ? smallIcon : largeIcon);
            AppWindow.SetIcon(iconId);
            AppWindow.SetTaskbarIcon(iconId);
        }
        catch (Exception ex)
        {
            TryWriteWindowDiagnostic($"Failed to assign AppWindow icon handles. {ex.GetType().Name}: {ex.Message}");
        }

        if (largeIcon != IntPtr.Zero)
        {
            SendMessage(windowHandle, WindowMessageSetIcon, (IntPtr)WindowIconLarge, largeIcon);
            SetClassLongPtr(windowHandle, WindowClassLongIcon, largeIcon);
        }

        if (smallIcon != IntPtr.Zero)
        {
            SendMessage(windowHandle, WindowMessageSetIcon, (IntPtr)WindowIconSmall, smallIcon);
            SetClassLongPtr(windowHandle, WindowClassLongSmallIcon, smallIcon);
        }
    }

    private void ReleaseWindowIcons()
    {
        DestroyUniqueIconHandles(_windowSmallIconHandle, _windowLargeIconHandle);
        _windowSmallIconHandle = IntPtr.Zero;
        _windowLargeIconHandle = IntPtr.Zero;
    }

    private static void DestroyUniqueIconHandles(params IntPtr[] iconHandles)
    {
        foreach (var iconHandle in iconHandles.Where(handle => handle != IntPtr.Zero).Distinct())
        {
            DestroyIcon(iconHandle);
        }
    }

    private static void TryWriteWindowDiagnostic(string message)
    {
        try
        {
            var paths = App.GetService<LocalAppPaths>();
            AppDiagnosticsLog.Write(paths, nameof(MainWindow), message);
        }
        catch
        {
        }
    }

    [DllImport("Shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconEx(
        string fileName,
        int iconIndex,
        IntPtr[] largeIcons,
        IntPtr[] smallIcons,
        uint iconCount);

    [DllImport("User32.dll", SetLastError = true)]
    private static extern IntPtr CopyIcon(IntPtr iconHandle);

    [DllImport("User32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("User32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr SetClassLongPtr(IntPtr windowHandle, int index, IntPtr newLong);

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr iconHandle);

    private void PrepareForClose()
    {
        if (_closeCleanupCompleted)
        {
            return;
        }

        _closeCleanupCompleted = true;
        Activated -= MainWindow_Activated;
        RootLayout.ActualThemeChanged -= RootLayout_ActualThemeChanged;
        RootLayout.SizeChanged -= RootLayout_SizeChanged;
        DashboardPanel.SizeChanged -= DashboardPanel_SizeChanged;
        OverviewPanel.SizeChanged -= OverviewPanel_SizeChanged;
        AppWindow.Closing -= AppWindow_Closing;
        ReleaseWindowIcons();
        VapourSynthWorkspacePanel.Dispose();
        _externalVapourSynthOpenLock.Dispose();
        ViewModel.Dispose();
    }

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr windowHandle);

    [DllImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
