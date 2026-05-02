using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FlowEncode.Application;
using FlowEncode.Domain;
using FlowEncode.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.System;
using WinRT.Interop;

namespace FlowEncode;

public sealed partial class VapourSynthPreviewWindow : Window
{
    private readonly Dictionary<int, PreviewOutputState> _outputStates = [];
    private readonly IVapourSynthPreviewService _previewService;
    private bool _isClosed;
    private bool _isFrameLoadInProgress;
    private bool _isFullScreenActive;
    private bool _isInternalControlUpdate;
    private bool _isPreviewPanActive;
    private bool _isPlaying;
    private bool _hasEverActivated;
    private Task? _closePreviewSessionTask;
    private int? _pendingFrameNumber;
    private uint _previewPanPointerId;
    private string? _pendingStatusText;
    private byte[]? _displayedFramePixels;
    private int _displayedFrameHeight;
    private int _displayedFrameWidth;
    private readonly List<TextBox> _attachedPreviewNumberBoxEditors = [];
    private XamlRoot? _observedXamlRoot;
    private Point _previewPanOrigin;
    private double _previewPanStartHorizontalOffset;
    private double _previewPanStartVerticalOffset;
    private VapourSynthPreviewFrameData? _lastFrameData;
    private byte[]? _sourceFramePixels;
    private int _sourceFrameHeight;
    private int _sourceFrameWidth;
    private VapourSynthPreviewOpenRequest? _currentRequest;
    private VapourSynthPreviewSessionInfo? _currentSession;
    private VapourSynthPreviewOutputInfo? _selectedOutputInfo;
    private int _activeChapterIndex = -1;

    public VapourSynthPreviewWindowViewModel ViewModel { get; }

    public event EventHandler? PreviewWindowClosed;

    public VapourSynthPreviewWindow(
        VapourSynthPreviewWindowViewModel viewModel,
        IVapourSynthPreviewService previewService)
    {
        ViewModel = viewModel;
        _previewService = previewService;
        InitializeComponent();

        if (Content is FrameworkElement root)
        {
            root.DataContext = ViewModel;
        }

        _playbackTimer.Tick += PlaybackTimer_Tick;
        _statusCommitTimer.Tick += StatusCommitTimer_Tick;
        Closed += VapourSynthPreviewWindow_Closed;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RootLayout.Loaded += RootLayout_Loaded;
        RootLayout.Unloaded += RootLayout_Unloaded;
        RootLayout.ActualThemeChanged += RootLayout_ActualThemeChanged;
        RootLayout.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootLayout_KeyDown), true);
        RootLayout.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(RootLayout_KeyUp), true);
        Title = ViewModel.WindowTitle;
    }

    private DispatcherTimer _playbackTimer { get; } = new();
    private DispatcherTimer _statusCommitTimer { get; } = new()
    {
        Interval = TimeSpan.FromMilliseconds(180)
    };

    public async Task<bool> OpenOrRefreshAsync(
        VapourSynthPreviewOpenRequest request,
        AppLanguage language,
        AppThemePreference themePreference)
    {
        var shouldResetChapters = _currentRequest is not null
            && !IsSamePreviewTarget(_currentRequest, request);
        _currentRequest = request;
        if (shouldResetChapters)
        {
            _activeChapterIndex = -1;
            ViewModel.ReplaceChapters([]);
        }

        ApplyPresentation(language, themePreference);
        var loaded = await LoadSessionAsync(preserveCurrentFrame: true);
        if (!loaded)
        {
            if (_hasEverActivated && !_isClosed)
            {
                Close();
            }

            return false;
        }

        Activate();
        _hasEverActivated = true;
        EnsurePreferredWindowPresentation();
        RefreshDisplayScale();
        return true;
    }

    private static bool IsSamePreviewTarget(
        VapourSynthPreviewOpenRequest left,
        VapourSynthPreviewOpenRequest right)
    {
        return string.Equals(
            BuildPreviewTargetKey(left),
            BuildPreviewTargetKey(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPreviewTargetKey(VapourSynthPreviewOpenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceFilePath))
        {
            try
            {
                return $"file:{Path.GetFullPath(request.SourceFilePath)}";
            }
            catch
            {
                return $"file:{request.SourceFilePath.Trim()}";
            }
        }

        return $"buffer:{request.WorkingDirectory}|{request.DisplayName}";
    }

    public async Task CloseForOwnerShutdownAsync()
    {
        if (_isClosed)
        {
            return;
        }

        await ClosePreviewSessionIfNeededAsync();
        Close();
    }

    public void ApplyPresentation(
        AppLanguage language,
        AppThemePreference themePreference)
    {
        ViewModel.ApplyLanguage(language);
        ApplyTheme(themePreference);
        RefreshDisplayScale();
        SyncControls();
        QueueAttachPreviewNumberBoxEditorHandlers();
    }

    private async Task<bool> LoadSessionAsync(bool preserveCurrentFrame)
    {
        if (_currentRequest is null)
        {
            return false;
        }

        StopPlayback();
        SaveCurrentOutputState();

        var previousOutputIndex = _selectedOutputInfo?.Index ?? 0;
        var previousFrame = preserveCurrentFrame ? ViewModel.CurrentFrame : 0;

        ViewModel.ResetForSession(
            _currentRequest.DisplayName,
            ViewModel.Texts.VapourSynthPreviewEvaluatingStatus(_currentRequest.DisplayName),
            []);
        _selectedOutputInfo = null;
        _lastFrameData = null;
        _sourceFramePixels = null;
        _sourceFrameWidth = 0;
        _sourceFrameHeight = 0;
        _displayedFramePixels = null;
        _displayedFrameWidth = 0;
        _displayedFrameHeight = 0;
        SyncControls();

        try
        {
            _currentSession = await _previewService.OpenSessionAsync(_currentRequest);
            ViewModel.ResetForSession(
                _currentRequest.DisplayName,
                ViewModel.Texts.VapourSynthPreviewSessionReadyStatus,
                _currentSession.Outputs);
            PruneOutputStates(_currentSession.Outputs);

            var output = _currentSession.Outputs.FirstOrDefault(item => item.Index == previousOutputIndex)
                ?? _currentSession.Outputs[0];
            await SelectOutputAsync(output, previousFrame, useExplicitFrame: true);
            return true;
        }
        catch (Exception ex)
        {
            _selectedOutputInfo = null;
            _currentSession = null;
            SetStatusText(ViewModel.Texts.VapourSynthPreviewRenderFailedStatus(ex.Message));
            SyncControls();
            return false;
        }
    }

    private async Task SelectOutputAsync(
        VapourSynthPreviewOutputInfo outputInfo,
        int? explicitFrameNumber = null,
        bool useExplicitFrame = false)
    {
        var previousOutputInfo = _selectedOutputInfo;
        SaveCurrentOutputState();
        var previousZoomState = previousOutputInfo is null
            ? null
            : CaptureZoomState(GetOrCreateOutputState(previousOutputInfo));

        var outputState = GetOrCreateOutputState(outputInfo);
        if (previousOutputInfo is not null && previousOutputInfo.Index != outputInfo.Index && previousZoomState is not null)
        {
            ApplyZoomState(outputState, previousZoomState);
        }

        _selectedOutputInfo = outputInfo;
        ViewModel.SelectedOutput = ViewModel.Outputs.FirstOrDefault(item => item.Info.Index == outputInfo.Index);
        ViewModel.UpdateForOutput(outputInfo);
        RestoreOutputPresentationState(outputState, outputInfo);
        SyncControls();

        var targetFrame = DetermineTargetFrame(
            previousOutputInfo,
            outputInfo,
            outputState,
            explicitFrameNumber,
            useExplicitFrame);
        await RenderFrameAsync(targetFrame);
    }

    private async Task RenderFrameAsync(int frameNumber)
    {
        if (_selectedOutputInfo is null)
        {
            return;
        }

        frameNumber = Math.Clamp(frameNumber, 0, Math.Max(0, _selectedOutputInfo.TotalFrames - 1));
        if (_isFrameLoadInProgress)
        {
            _pendingFrameNumber = frameNumber;
            return;
        }

        _isFrameLoadInProgress = true;
        _pendingFrameNumber = null;

        try
        {
            var frameData = await _previewService.RenderFrameAsync(_selectedOutputInfo.Index, frameNumber);
            _lastFrameData = frameData;
            _sourceFramePixels = await LoadFramePixelsAsync(frameData.RawPixelPath);
            _sourceFrameWidth = frameData.Width;
            _sourceFrameHeight = frameData.Height;

            await RefreshDisplayedFrameAsync();
            QueueFrameStatusText(ViewModel.Texts.VapourSynthPreviewReadyStatus(_selectedOutputInfo.Index, frameNumber));
            SyncControls();
        }
        catch (Exception ex)
        {
            StopPlayback();
            SetStatusText(ViewModel.Texts.VapourSynthPreviewRenderFailedStatus(ex.Message));
        }
        finally
        {
            _isFrameLoadInProgress = false;
        }

        if (_pendingFrameNumber is int pendingFrameNumber && pendingFrameNumber != ViewModel.CurrentFrame)
        {
            _pendingFrameNumber = null;
            await RenderFrameAsync(pendingFrameNumber);
        }
    }

    private async Task RefreshDisplayedFrameAsync()
    {
        if (_selectedOutputInfo is null || _lastFrameData is null || _sourceFramePixels is null)
        {
            return;
        }

        var displayPayload = await CreateDisplayFramePayloadAsync(
            _selectedOutputInfo,
            _lastFrameData,
            _sourceFramePixels,
            _sourceFrameWidth,
            _sourceFrameHeight);

        _displayedFramePixels = displayPayload.Pixels;
        _displayedFrameWidth = displayPayload.Width;
        _displayedFrameHeight = displayPayload.Height;
        ViewModel.UpdateCropPreviewState(ViewModel.IsCropPanelVisible);
        ViewModel.UpdateFrame(
            _selectedOutputInfo,
            _lastFrameData,
            displayPayload.Bitmap,
            displayPayload.Width,
            displayPayload.Height,
            displayPayload.ResolutionText);
        SaveCurrentOutputState();
    }

    private async Task<DisplayFramePayload> CreateDisplayFramePayloadAsync(
        VapourSynthPreviewOutputInfo outputInfo,
        VapourSynthPreviewFrameData frameData,
        byte[] sourcePixels,
        int sourceWidth,
        int sourceHeight)
    {
        var outputState = GetOrCreateOutputState(outputInfo);
        var cropBounds = GetEffectiveCropBounds(outputState, sourceWidth, sourceHeight);

        byte[] displayPixels;
        int displayWidth;
        int displayHeight;

        if (ViewModel.IsCropPanelVisible)
        {
            displayWidth = cropBounds.Width;
            displayHeight = cropBounds.Height;

            if (cropBounds.Left == 0
                && cropBounds.Top == 0
                && displayWidth == sourceWidth
                && displayHeight == sourceHeight)
            {
                displayPixels = sourcePixels;
            }
            else
            {
                displayPixels = CropPixels(
                    sourcePixels,
                    sourceWidth,
                    cropBounds.Left,
                    cropBounds.Top,
                    displayWidth,
                    displayHeight);
            }
        }
        else
        {
            displayPixels = sourcePixels;
            displayWidth = sourceWidth;
            displayHeight = sourceHeight;
        }

        var bitmap = await CreateBitmapAsync(displayPixels, displayWidth, displayHeight);
        var resolutionText = ViewModel.IsCropPanelVisible
            && (displayWidth != frameData.Width || displayHeight != frameData.Height)
            ? $"{frameData.Width} x {frameData.Height} -> {displayWidth} x {displayHeight}"
            : $"{frameData.Width} x {frameData.Height}";

        return new DisplayFramePayload(
            bitmap,
            displayPixels,
            displayWidth,
            displayHeight,
            resolutionText);
    }

    private static async Task<byte[]> LoadFramePixelsAsync(string rawPath)
    {
        var pixels = await File.ReadAllBytesAsync(rawPath);
        TryDeleteFile(rawPath);
        return pixels;
    }

    private static async Task<WriteableBitmap> CreateBitmapAsync(byte[] pixels, int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels);
        await stream.FlushAsync();
        return bitmap;
    }

    private void SyncControls()
    {
        _isInternalControlUpdate = true;
        DetachControlEvents();

        try
        {
            OutputSelectorComboBox.SelectedItem = ViewModel.SelectedOutput;
            FrameNumberBox.Maximum = Math.Max(0, ViewModel.TotalFrames - 1);
            FrameNumberBox.Value = ViewModel.CurrentFrame;
            FrameSlider.Maximum = ViewModel.FrameSliderMaximum;
            FrameSlider.Value = ViewModel.CurrentFrame;
            UpdateActiveChapter(ViewModel.CurrentFrame);
            RedrawChapterMarkers();
            StepSizeBox.Value = ViewModel.StepSize;
            ZoomRatioBox.Value = Math.Round(ViewModel.ZoomRatio * 100);
            ZoomRatioBox.Visibility = ViewModel.CustomZoomVisibility;
            FramePropsPanel.Visibility = ViewModel.IsFramePropsVisible ? Visibility.Visible : Visibility.Collapsed;
            PropsColumn.Width = ViewModel.IsFramePropsVisible ? new GridLength(300) : new GridLength(0);
            FramePropsToggleButton.IsChecked = ViewModel.IsFramePropsVisible;
            CropToggleButton.IsChecked = ViewModel.IsCropPanelVisible;
            TimelineModeComboBox.SelectedItem = ViewModel.SelectedTimelineMode;
            TimeStepSecondsBox.Value = ViewModel.TimeStepSeconds;
            CropModeComboBox.SelectedItem = ViewModel.SelectedCropMode;
            ChapterSelectorComboBox.SelectedItem = _activeChapterIndex >= 0 && _activeChapterIndex < ViewModel.Chapters.Count
                ? ViewModel.Chapters[_activeChapterIndex]
                : null;
            Title = ViewModel.WindowTitle;

            if (_selectedOutputInfo is not null)
            {
                ApplyCropControlsFromState(GetOrCreateOutputState(_selectedOutputInfo), _selectedOutputInfo);
            }
            else
            {
                ResetCropControls();
            }
        }
        finally
        {
            AttachControlEvents();
            _isInternalControlUpdate = false;
        }
    }

    private void ApplyTheme(AppThemePreference preference)
    {
        RootLayout.RequestedTheme = preference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private async void ReloadPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var loaded = await LoadSessionAsync(preserveCurrentFrame: true);
        if (!loaded && !_isClosed)
        {
            Close();
        }
    }

    private async void SaveSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentSnapshotAsync();
    }

    private async void SaveAllOutputsButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveAllOutputsAtCurrentFrameAsync();
    }

    private async Task SaveCurrentSnapshotAsync()
    {
        if (_displayedFramePixels is null || _displayedFrameWidth <= 0 || _displayedFrameHeight <= 0)
        {
            return;
        }

        if (ViewModel.SilentSnapshotEnabled)
        {
            try
            {
                var snapshotPath = ResolveSilentSnapshotPath();
                Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
                await SavePixelsAsPngAsync(snapshotPath, _displayedFramePixels, _displayedFrameWidth, _displayedFrameHeight);
                SetStatusText(ViewModel.Texts.VapourSynthPreviewSnapshotSavedStatus(snapshotPath));
                return;
            }
            catch (Exception ex)
            {
                SetStatusText(ViewModel.Texts.VapourSynthPreviewSnapshotTemplateFailedStatus(ex.Message));
            }
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            SuggestedFileName = BuildShortcutSnapshotFileName(withExtension: false),
            DefaultFileExtension = ".png"
        };
        picker.FileTypeChoices.Add(ViewModel.Texts.VapourSynthPreviewSnapshotFileTypeDescription, [".png"]);
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await SavePixelsAsPngAsync(file.Path, _displayedFramePixels, _displayedFrameWidth, _displayedFrameHeight);
        SetStatusText(ViewModel.Texts.VapourSynthPreviewSnapshotSavedStatus(file.Path));
    }

    private async Task QuickSaveSnapshotAsync()
    {
        if (_displayedFramePixels is null || _displayedFrameWidth <= 0 || _displayedFrameHeight <= 0)
        {
            return;
        }

        try
        {
            var picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, GetWindowHandle());

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            var snapshotPath = Path.Combine(folder.Path, BuildShortcutSnapshotFileName(withExtension: true));
            await SavePixelsAsPngAsync(snapshotPath, _displayedFramePixels, _displayedFrameWidth, _displayedFrameHeight);
            SetStatusText(ViewModel.Texts.VapourSynthPreviewSnapshotSavedStatus(snapshotPath));
        }
        catch (Exception ex)
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewSnapshotTemplateFailedStatus(ex.Message));
        }
    }

    private async void CopyFrameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_displayedFramePixels is null || _displayedFrameWidth <= 0 || _displayedFrameHeight <= 0)
        {
            return;
        }

        var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)_displayedFrameWidth,
            (uint)_displayedFrameHeight,
            96,
            96,
            _displayedFramePixels);
        await encoder.FlushAsync();
        stream.Seek(0);

        var dataPackage = new DataPackage();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
        SetStatusText(ViewModel.Texts.VapourSynthPreviewFrameCopiedStatus);
    }

    private void ReturnToEditorButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FramePropsToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsFramePropsVisible = true;
        SyncControls();
    }

    private void FramePropsToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsFramePropsVisible = false;
        SyncControls();
    }

    private async void CropToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCropPanelVisible = true;
        await RefreshDisplayedFrameAsync();
        SyncControls();
        await ScrollToCropBoundaryAsync(null);
    }

    private async void CropToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCropPanelVisible = false;
        await RefreshDisplayedFrameAsync();
        SyncControls();
    }

    private async void AdvancedSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowAdvancedSettingsAsync();
    }

    private async void OutputSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInternalControlUpdate
            || OutputSelectorComboBox.SelectedItem is not VapourSynthPreviewOutputOption option)
        {
            return;
        }

        await SelectOutputAsync(option.Info);
    }

    private async void FrameNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isInternalControlUpdate || double.IsNaN(sender.Value))
        {
            return;
        }

        var requestedFrame = (int)Math.Round(sender.Value);
        if (requestedFrame == ViewModel.CurrentFrame)
        {
            return;
        }

        await RenderFrameAsync(requestedFrame);
    }

    private async void FrameNumberBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isInternalControlUpdate || double.IsNaN(FrameNumberBox.Value))
        {
            return;
        }

        await RenderFrameAsync((int)Math.Round(FrameNumberBox.Value));
    }

    private void ZoomModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInternalControlUpdate)
        {
            return;
        }

        SaveCurrentOutputState();
        SyncControls();
    }

    private void ZoomRatioBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isInternalControlUpdate || double.IsNaN(sender.Value))
        {
            return;
        }

        ViewModel.ZoomRatio = Math.Max(5, sender.Value) / 100.0;
        SaveCurrentOutputState();
    }

    private void TimelineModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInternalControlUpdate)
        {
            return;
        }

        SyncControls();
    }

    private void TimeStepSecondsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isInternalControlUpdate || double.IsNaN(sender.Value))
        {
            return;
        }

        ViewModel.TimeStepSeconds = Math.Max(0.001, sender.Value);
    }

    private async void TimeStepBackButton_Click(object sender, RoutedEventArgs e)
    {
        await StepByTimeAsync(-ViewModel.TimeStepSeconds);
    }

    private async void TimeStepForwardButton_Click(object sender, RoutedEventArgs e)
    {
        await StepByTimeAsync(ViewModel.TimeStepSeconds);
    }

    private async void CropModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInternalControlUpdate || _selectedOutputInfo is null)
        {
            return;
        }

        var outputState = GetOrCreateOutputState(_selectedOutputInfo);
        outputState.CropMode = ViewModel.SelectedCropMode?.Value ?? "relative";
        EnsureCropStatesWithinBounds(outputState, _selectedOutputInfo.Width, _selectedOutputInfo.Height);
        ViewModel.CropZoomPercentage = GetActiveCropZoomPercentage(outputState);
        ViewModel.UpdateCropPreviewState(ViewModel.IsCropPanelVisible);
        SyncControls();
        await RefreshDisplayedFrameAsync();
    }

    private async void CropLeftBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Left, sender);
    }

    private async void CropTopBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Top, sender);
    }

    private async void CropWidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Width, sender);
    }

    private async void CropHeightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Height, sender);
    }

    private async void CropRightBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Right, sender);
    }

    private async void CropBottomBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        await UpdateCropValueAsync(CropField.Bottom, sender);
    }

    private void CropZoomBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isInternalControlUpdate || _selectedOutputInfo is null || double.IsNaN(sender.Value))
        {
            return;
        }

        var outputState = GetOrCreateOutputState(_selectedOutputInfo);
        SetActiveCropZoomPercentage(outputState, Math.Clamp(sender.Value, 10, 800));
        ViewModel.CropZoomPercentage = GetActiveCropZoomPercentage(outputState);
        ViewModel.UpdateCropPreviewState(ViewModel.IsCropPanelVisible);
        SaveCurrentOutputState();
    }

    private void CopyCropCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOutputInfo is null)
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(BuildCropCommandSnippet(useSnippetPlaceholder: false));
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
        SetStatusText(ViewModel.Texts.VapourSynthPreviewCropCommandCopiedStatus);
    }

    private void PreviewViewportHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshDisplayScale();
        ViewModel.UpdateViewport(
            Math.Max(0, e.NewSize.Width - 16),
            Math.Max(0, e.NewSize.Height - 16));
    }

    private void PreviewScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (e.Handled || !IsControlKeyPressed() || _displayedFrameWidth <= 0 || _displayedFrameHeight <= 0)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(PreviewScrollViewer);
        var wheelDelta = currentPoint.Properties.MouseWheelDelta;
        if (wheelDelta == 0)
        {
            return;
        }

        var previousWidth = ViewModel.PreviewImageWidth;
        var previousHeight = ViewModel.PreviewImageHeight;
        var pointerPosition = currentPoint.Position;
        var anchorXRatio = previousWidth > 0
            ? Math.Clamp((PreviewScrollViewer.HorizontalOffset + pointerPosition.X) / previousWidth, 0.0, 1.0)
            : 0.5;
        var anchorYRatio = previousHeight > 0
            ? Math.Clamp((PreviewScrollViewer.VerticalOffset + pointerPosition.Y) / previousHeight, 0.0, 1.0)
            : 0.5;

        var factor = Math.Pow(1.1, wheelDelta / 120.0);
        var targetZoomRatio = Math.Clamp(GetCurrentEffectiveZoomRatio() * factor, 0.05, 16.0);
        ApplyCustomZoom(targetZoomRatio);
        RestorePreviewScrollAnchor(anchorXRatio, anchorYRatio, pointerPosition.X, pointerPosition.Y);
        QueueFocusPreviewSurface();
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Handled)
        {
            QueueFocusPreviewSurface();
        }

        if (e.Handled || !CanPanPreview())
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(PreviewScrollViewer);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPreviewPanActive = true;
        _previewPanPointerId = currentPoint.PointerId;
        _previewPanOrigin = currentPoint.Position;
        _previewPanStartHorizontalOffset = PreviewScrollViewer.HorizontalOffset;
        _previewPanStartVerticalOffset = PreviewScrollViewer.VerticalOffset;
        PreviewScrollViewer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPreviewPanActive || e.Pointer.PointerId != _previewPanPointerId)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(PreviewScrollViewer);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            ReleasePreviewPanCapture();
            return;
        }

        var deltaX = currentPoint.Position.X - _previewPanOrigin.X;
        var deltaY = currentPoint.Position.Y - _previewPanOrigin.Y;
        var targetHorizontalOffset = Math.Clamp(
            _previewPanStartHorizontalOffset - deltaX,
            0,
            PreviewScrollViewer.ScrollableWidth);
        var targetVerticalOffset = Math.Clamp(
            _previewPanStartVerticalOffset - deltaY,
            0,
            PreviewScrollViewer.ScrollableHeight);

        PreviewScrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null, true);
        e.Handled = true;
    }

    private void PreviewScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPreviewPanActive && e.Pointer.PointerId == _previewPanPointerId)
        {
            ReleasePreviewPanCapture();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_isPreviewPanActive && e.Pointer.PointerId == _previewPanPointerId)
        {
            ReleasePreviewPanCapture();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_isPreviewPanActive && e.Pointer.PointerId == _previewPanPointerId)
        {
            ReleasePreviewPanCapture();
        }
    }

    private async void FirstFrameButton_Click(object sender, RoutedEventArgs e)
    {
        await RenderFrameAsync(0);
    }

    private async void StepBackButton_Click(object sender, RoutedEventArgs e)
    {
        await RenderFrameAsync(Math.Max(0, ViewModel.CurrentFrame - ViewModel.StepSize));
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            StopPlayback(updateStatus: true);
            return;
        }

        if (_selectedOutputInfo is null)
        {
            return;
        }

        var fps = _selectedOutputInfo.FpsNumerator > 0 && _selectedOutputInfo.FpsDenominator > 0
            ? _selectedOutputInfo.FpsNumerator / (double)_selectedOutputInfo.FpsDenominator
            : 24.0;
        _playbackTimer.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(1.0, fps));
        _isPlaying = true;
        PlayPauseIcon.Symbol = Symbol.Pause;
        SetStatusText(ViewModel.Texts.VapourSynthPreviewPlaybackStatus);
        _playbackTimer.Start();
    }

    private async void StepForwardButton_Click(object sender, RoutedEventArgs e)
    {
        await RenderFrameAsync(Math.Min(Math.Max(0, ViewModel.TotalFrames - 1), ViewModel.CurrentFrame + ViewModel.StepSize));
    }

    private async void LastFrameButton_Click(object sender, RoutedEventArgs e)
    {
        await RenderFrameAsync(Math.Max(0, ViewModel.TotalFrames - 1));
    }

    private void StepSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_isInternalControlUpdate || double.IsNaN(sender.Value))
        {
            return;
        }

        ViewModel.StepSize = (int)Math.Max(1, Math.Round(sender.Value));
    }

    private async void FrameSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInternalControlUpdate)
        {
            return;
        }

        var frameNumber = (int)Math.Round(e.NewValue);
        UpdateActiveChapter(frameNumber);
        await RenderFrameAsync(frameNumber);
    }

    private async void FrameSliderHost_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedOutputInfo is null || ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var point = e.GetCurrentPoint(FrameSliderHost).Position;
        var track = MeasureSliderTrackBounds();
        if (track.width <= 0)
        {
            return;
        }

        var clickedFrame = (int)Math.Round(Math.Clamp((point.X - track.offset) / track.width, 0, 1) * ViewModel.FrameSliderMaximum);
        var nearest = ViewModel.Chapters
            .Select((chapter, index) => new
            {
                Chapter = chapter,
                Index = index,
                Frame = TimecodeToFrame(chapter.Timecode, _selectedOutputInfo)
            })
            .OrderBy(item => Math.Abs(item.Frame - clickedFrame))
            .FirstOrDefault();

        if (nearest is null)
        {
            return;
        }

        var toleranceFrames = Math.Max(3, (int)Math.Round(GetOutputFps(_selectedOutputInfo) * 0.5));
        if (Math.Abs(nearest.Frame - clickedFrame) > toleranceFrames)
        {
            return;
        }

        _activeChapterIndex = nearest.Index;
        RedrawChapterMarkers();
        e.Handled = true;
        await RenderFrameAsync(nearest.Frame);
    }

    private void ChapterMarkerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawChapterMarkers();
    }

    private async void ChapterSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInternalControlUpdate
            || _selectedOutputInfo is null
            || ChapterSelectorComboBox.SelectedItem is not VapourSynthPreviewChapterOption chapter)
        {
            return;
        }

        var index = ViewModel.Chapters.IndexOf(chapter);
        if (index < 0)
        {
            return;
        }

        _activeChapterIndex = index;
        RedrawChapterMarkers();
        await RenderFrameAsync(TimecodeToFrame(chapter.Timecode, _selectedOutputInfo));
    }

    private async void ImportChapterButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".xml");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var chapters = await LoadChapterFileAsync(file.Path);
            ViewModel.ReplaceChapters(BuildChapterOptions(chapters));
            _activeChapterIndex = -1;
            UpdateChapterButtons();
            RedrawChapterMarkers();
            SetStatusText(ViewModel.Chapters.Count > 0
                ? ViewModel.Texts.VapourSynthPreviewChaptersImportedStatus(ViewModel.Chapters.Count)
                : ViewModel.Texts.VapourSynthPreviewNoChaptersFoundStatus);
        }
        catch (Exception ex)
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterImportFailedStatus(ex.Message));
        }
    }

    private async void ExportChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "chapters",
            DefaultFileExtension = ".txt"
        };
        picker.FileTypeChoices.Add(ViewModel.Texts.VapourSynthPreviewOgmChapterFileTypeDescription, [".txt"]);
        picker.FileTypeChoices.Add(ViewModel.Texts.VapourSynthPreviewXmlChapterFileTypeDescription, [".xml"]);
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var chapters = GetChapterEntries();
            if (string.Equals(Path.GetExtension(file.Path), ".xml", StringComparison.OrdinalIgnoreCase))
            {
                await File.WriteAllTextAsync(file.Path, BuildMatroskaChaptersXml(chapters));
            }
            else
            {
                await File.WriteAllLinesAsync(file.Path, BuildOgmChapterLines(chapters));
            }

            SetStatusText(ViewModel.Texts.VapourSynthPreviewChaptersExportedStatus(chapters.Count, file.Path));
        }
        catch (Exception ex)
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterExportFailedStatus(ex.Message));
        }
    }

    private async void AddChapterButton_Click(object sender, RoutedEventArgs e)
    {
        var timecode = CurrentFrameToTimecode();
        var title = ViewModel.Texts.VapourSynthPreviewChapterFallbackTitle(ViewModel.Chapters.Count + 1);
        var result = await ShowChapterEditDialogAsync(timecode, title);
        if (result is null)
        {
            return;
        }

        var chapters = GetChapterEntries();
        chapters.Add(result);
        ViewModel.ReplaceChapters(BuildChapterOptions(chapters));
        UpdateActiveChapter(TimecodeToFrame(result.Timecode, _selectedOutputInfo));
        UpdateChapterButtons();
        RedrawChapterMarkers();
        SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterAddedStatus(result.Title));
        await RenderFrameAsync(TimecodeToFrame(result.Timecode, _selectedOutputInfo));
    }

    private async void EditChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var index = ResolveActiveChapterIndex();
        if (index < 0)
        {
            return;
        }

        var current = ViewModel.Chapters[index];
        var result = await ShowChapterEditDialogAsync(current.Timecode, current.Title);
        if (result is null)
        {
            return;
        }

        var chapters = GetChapterEntries();
        chapters[index] = result;
        ViewModel.ReplaceChapters(BuildChapterOptions(chapters));
        UpdateActiveChapter(TimecodeToFrame(result.Timecode, _selectedOutputInfo));
        UpdateChapterButtons();
        RedrawChapterMarkers();
        SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterUpdatedStatus(result.Title));
        await RenderFrameAsync(TimecodeToFrame(result.Timecode, _selectedOutputInfo));
    }

    private void DeleteChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var index = ResolveActiveChapterIndex();
        if (index < 0)
        {
            return;
        }

        var chapters = GetChapterEntries();
        var title = chapters[index].Title;
        chapters.RemoveAt(index);
        ViewModel.ReplaceChapters(BuildChapterOptions(chapters));
        _activeChapterIndex = -1;
        UpdateChapterButtons();
        RedrawChapterMarkers();
        SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterDeletedStatus(title));
    }

    private async void NextChapterButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var currentFrame = ViewModel.CurrentFrame;
        var next = ViewModel.Chapters
            .Select((chapter, index) => new
            {
                Chapter = chapter,
                Index = index,
                Frame = TimecodeToFrame(chapter.Timecode, _selectedOutputInfo)
            })
            .Where(item => item.Frame > currentFrame)
            .OrderBy(item => item.Frame)
            .FirstOrDefault()
            ?? ViewModel.Chapters
                .Select((chapter, index) => new
                {
                    Chapter = chapter,
                    Index = index,
                    Frame = TimecodeToFrame(chapter.Timecode, _selectedOutputInfo)
                })
                .OrderBy(item => item.Frame)
                .First();

        _activeChapterIndex = next.Index;
        RedrawChapterMarkers();
        await RenderFrameAsync(next.Frame);
    }

    private async void PlaybackTimer_Tick(object? sender, object e)
    {
        if (_isFrameLoadInProgress)
        {
            return;
        }

        if (_selectedOutputInfo is null || ViewModel.CurrentFrame >= Math.Max(0, ViewModel.TotalFrames - 1))
        {
            StopPlayback(updateStatus: true);
            return;
        }

        await RenderFrameAsync(ViewModel.CurrentFrame + 1);
    }

    private async void VapourSynthPreviewWindow_Closed(object sender, WindowEventArgs args)
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        StopPlayback();
        SaveCurrentOutputState();
        DetachXamlRoot();
        DetachPreviewNumberBoxEditorHandlers();
        RootLayout.Loaded -= RootLayout_Loaded;
        RootLayout.Unloaded -= RootLayout_Unloaded;
        RootLayout.ActualThemeChanged -= RootLayout_ActualThemeChanged;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        await ClosePreviewSessionIfNeededAsync();
        PreviewWindowClosed?.Invoke(this, EventArgs.Empty);
    }

    private Task ClosePreviewSessionIfNeededAsync()
    {
        _closePreviewSessionTask ??= _previewService.CloseSessionAsync();
        return _closePreviewSessionTask;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ViewModel.WindowTitle), StringComparison.Ordinal))
        {
            Title = ViewModel.WindowTitle;
        }
    }

    private async Task UpdateCropValueAsync(CropField field, NumberBox sender)
    {
        if (_isInternalControlUpdate || _selectedOutputInfo is null || double.IsNaN(sender.Value))
        {
            return;
        }

        var outputState = GetOrCreateOutputState(_selectedOutputInfo);
        if (IsAbsoluteCropMode(outputState.CropMode))
        {
            ApplyAbsoluteCropFieldValue(
                outputState.AbsoluteCrop,
                field,
                (int)Math.Round(sender.Value),
                _selectedOutputInfo.Width,
                _selectedOutputInfo.Height);
        }
        else
        {
            ApplyRelativeCropFieldValue(
                outputState.RelativeCrop,
                field,
                (int)Math.Round(sender.Value),
                _selectedOutputInfo.Width,
                _selectedOutputInfo.Height);
        }

        SyncControls();
        await RefreshDisplayedFrameAsync();
        await ScrollToCropBoundaryAsync(field);
    }

    private async Task StepByTimeAsync(double deltaSeconds)
    {
        if (_selectedOutputInfo is null)
        {
            return;
        }

        var fps = GetOutputFps(_selectedOutputInfo);
        var frameDelta = Math.Max(1, (int)Math.Round(Math.Abs(deltaSeconds) * fps));
        var targetFrame = deltaSeconds < 0
            ? ViewModel.CurrentFrame - frameDelta
            : ViewModel.CurrentFrame + frameDelta;
        await RenderFrameAsync(targetFrame);
    }

    private void SaveCurrentOutputState()
    {
        if (_selectedOutputInfo is null)
        {
            return;
        }

        var outputState = GetOrCreateOutputState(_selectedOutputInfo);
        outputState.HasVisited = true;
        outputState.CurrentFrame = ViewModel.CurrentFrame;
        outputState.ZoomMode = ViewModel.SelectedZoomMode?.Value ?? "actual";
        outputState.ZoomRatio = ViewModel.ZoomRatio;
        outputState.CropMode = ViewModel.SelectedCropMode?.Value ?? "relative";

        if (IsAbsoluteCropMode(outputState.CropMode))
        {
            outputState.AbsoluteCrop.Left = GetIntValue(CropLeftBox);
            outputState.AbsoluteCrop.Top = GetIntValue(CropTopBox);
            outputState.AbsoluteCrop.Width = Math.Max(1, GetIntValue(CropWidthBox));
            outputState.AbsoluteCrop.Height = Math.Max(1, GetIntValue(CropHeightBox));
            outputState.AbsoluteCrop.ZoomPercentage = Math.Clamp(CropZoomBox.Value, 10, 800);
        }
        else
        {
            outputState.RelativeCrop.Left = GetIntValue(CropLeftBox);
            outputState.RelativeCrop.Top = GetIntValue(CropTopBox);
            outputState.RelativeCrop.Right = GetIntValue(CropRightBox);
            outputState.RelativeCrop.Bottom = GetIntValue(CropBottomBox);
            outputState.RelativeCrop.ZoomPercentage = Math.Clamp(CropZoomBox.Value, 10, 800);
        }

        EnsureCropStatesWithinBounds(outputState, _selectedOutputInfo.Width, _selectedOutputInfo.Height);
    }

    private void RestoreOutputPresentationState(PreviewOutputState outputState, VapourSynthPreviewOutputInfo outputInfo)
    {
        EnsureCropStatesWithinBounds(outputState, outputInfo.Width, outputInfo.Height);
        ViewModel.SelectedZoomMode = ViewModel.ZoomModes.FirstOrDefault(option => option.Value == outputState.ZoomMode)
            ?? ViewModel.ZoomModes[0];
        ViewModel.ZoomRatio = outputState.ZoomRatio;
        ViewModel.SelectedCropMode = ViewModel.CropModes.FirstOrDefault(option => option.Value == outputState.CropMode)
            ?? ViewModel.CropModes[0];
        ViewModel.CropZoomPercentage = GetActiveCropZoomPercentage(outputState);
        ViewModel.UpdateCropPreviewState(ViewModel.IsCropPanelVisible);
    }

    private static PreviewZoomState CaptureZoomState(PreviewOutputState outputState)
    {
        return new PreviewZoomState(outputState.ZoomMode, outputState.ZoomRatio);
    }

    private static void ApplyZoomState(PreviewOutputState outputState, PreviewZoomState zoomState)
    {
        outputState.ZoomMode = zoomState.ZoomMode;
        outputState.ZoomRatio = zoomState.ZoomRatio;
    }

    private int DetermineTargetFrame(
        VapourSynthPreviewOutputInfo? previousOutputInfo,
        VapourSynthPreviewOutputInfo targetOutputInfo,
        PreviewOutputState targetOutputState,
        int? explicitFrameNumber,
        bool useExplicitFrame)
    {
        var fallbackFrame = Math.Clamp(explicitFrameNumber ?? ViewModel.CurrentFrame, 0, Math.Max(0, targetOutputInfo.TotalFrames - 1));
        if (useExplicitFrame)
        {
            return fallbackFrame;
        }

        if (previousOutputInfo is null || previousOutputInfo.Index == targetOutputInfo.Index)
        {
            return targetOutputState.HasVisited
                ? Math.Clamp(targetOutputState.CurrentFrame, 0, Math.Max(0, targetOutputInfo.TotalFrames - 1))
                : fallbackFrame;
        }

        var syncMode = ViewModel.SelectedOutputSyncMode?.Value ?? "remember";
        return syncMode switch
        {
            "frame" => fallbackFrame,
            "time" => ConvertFrameByTimestamp(previousOutputInfo, targetOutputInfo, explicitFrameNumber ?? ViewModel.CurrentFrame),
            "timeline" => ViewModel.IsTimelineTimeMode
                ? ConvertFrameByTimestamp(previousOutputInfo, targetOutputInfo, explicitFrameNumber ?? ViewModel.CurrentFrame)
                : fallbackFrame,
            _ => targetOutputState.HasVisited
                ? Math.Clamp(targetOutputState.CurrentFrame, 0, Math.Max(0, targetOutputInfo.TotalFrames - 1))
                : fallbackFrame
        };
    }

    private static int ConvertFrameByTimestamp(
        VapourSynthPreviewOutputInfo sourceOutput,
        VapourSynthPreviewOutputInfo targetOutput,
        int sourceFrameNumber)
    {
        var timestamp = GetFrameTimestampSeconds(sourceFrameNumber, sourceOutput);
        var fps = GetOutputFps(targetOutput);
        return Math.Clamp(
            (int)Math.Round(timestamp * fps),
            0,
            Math.Max(0, targetOutput.TotalFrames - 1));
    }

    private static double GetFrameTimestampSeconds(int frameNumber, VapourSynthPreviewOutputInfo outputInfo)
    {
        if (outputInfo.FpsNumerator <= 0 || outputInfo.FpsDenominator <= 0)
        {
            return frameNumber / 24.0;
        }

        return frameNumber / (outputInfo.FpsNumerator / (double)outputInfo.FpsDenominator);
    }

    private TimeSpan CurrentFrameToTimecode()
    {
        return FrameToTimecode(ViewModel.CurrentFrame, _selectedOutputInfo);
    }

    private static TimeSpan FrameToTimecode(int frameNumber, VapourSynthPreviewOutputInfo? outputInfo)
    {
        var fps = outputInfo is null ? 24.0 : GetOutputFps(outputInfo);
        return TimeSpan.FromSeconds(Math.Max(0, frameNumber) / fps);
    }

    private static int TimecodeToFrame(TimeSpan timecode, VapourSynthPreviewOutputInfo? outputInfo)
    {
        var fps = outputInfo is null ? 24.0 : GetOutputFps(outputInfo);
        var maxFrame = outputInfo is null ? int.MaxValue : Math.Max(0, outputInfo.TotalFrames - 1);
        return Math.Clamp((int)Math.Round(Math.Max(0, timecode.TotalSeconds) * fps), 0, maxFrame);
    }

    private static double GetOutputFps(VapourSynthPreviewOutputInfo outputInfo)
    {
        return outputInfo.FpsNumerator > 0 && outputInfo.FpsDenominator > 0
            ? outputInfo.FpsNumerator / (double)outputInfo.FpsDenominator
            : 24.0;
    }

    private PreviewOutputState GetOrCreateOutputState(VapourSynthPreviewOutputInfo outputInfo)
    {
        if (!_outputStates.TryGetValue(outputInfo.Index, out var outputState))
        {
            outputState = PreviewOutputState.CreateDefault(outputInfo.Width, outputInfo.Height);
            _outputStates[outputInfo.Index] = outputState;
        }

        EnsureCropStatesWithinBounds(outputState, outputInfo.Width, outputInfo.Height);
        return outputState;
    }

    private void PruneOutputStates(IReadOnlyList<VapourSynthPreviewOutputInfo> outputs)
    {
        var existingIndices = outputs.Select(static output => output.Index).ToHashSet();
        var obsolete = _outputStates.Keys.Where(index => !existingIndices.Contains(index)).ToArray();
        foreach (var index in obsolete)
        {
            _outputStates.Remove(index);
        }
    }

    private void ApplyCropControlsFromState(PreviewOutputState outputState, VapourSynthPreviewOutputInfo outputInfo)
    {
        var absoluteMode = IsAbsoluteCropMode(outputState.CropMode);
        var cropBounds = GetEffectiveCropBounds(outputState, outputInfo.Width, outputInfo.Height);

        CropLeftBox.Minimum = 0;
        CropLeftBox.Maximum = Math.Max(0, outputInfo.Width - 1);
        CropTopBox.Minimum = 0;
        CropTopBox.Maximum = Math.Max(0, outputInfo.Height - 1);
        CropWidthBox.Minimum = 1;
        CropWidthBox.Maximum = outputInfo.Width;
        CropHeightBox.Minimum = 1;
        CropHeightBox.Maximum = outputInfo.Height;
        CropRightBox.Minimum = 0;
        CropRightBox.Maximum = Math.Max(0, outputInfo.Width - 1);
        CropBottomBox.Minimum = 0;
        CropBottomBox.Maximum = Math.Max(0, outputInfo.Height - 1);

        CropLeftBox.Value = cropBounds.Left;
        CropTopBox.Value = cropBounds.Top;
        CropWidthBox.Value = cropBounds.Width;
        CropHeightBox.Value = cropBounds.Height;
        CropRightBox.Value = cropBounds.Right;
        CropBottomBox.Value = cropBounds.Bottom;
        CropZoomBox.Value = GetActiveCropZoomPercentage(outputState);

        CropWidthBox.IsEnabled = absoluteMode;
        CropHeightBox.IsEnabled = absoluteMode;
        CropRightBox.IsEnabled = !absoluteMode;
        CropBottomBox.IsEnabled = !absoluteMode;
    }

    private void ResetCropControls()
    {
        CropLeftBox.Value = 0;
        CropTopBox.Value = 0;
        CropWidthBox.Value = 1;
        CropHeightBox.Value = 1;
        CropRightBox.Value = 0;
        CropBottomBox.Value = 0;
        CropZoomBox.Value = 100;
    }

    private static void EnsureCropStatesWithinBounds(PreviewOutputState outputState, int sourceWidth, int sourceHeight)
    {
        EnsureAbsoluteCropStateWithinBounds(outputState.AbsoluteCrop, sourceWidth, sourceHeight);
        EnsureRelativeCropStateWithinBounds(outputState.RelativeCrop, sourceWidth, sourceHeight);
    }

    private static void EnsureAbsoluteCropStateWithinBounds(AbsoluteCropState cropState, int sourceWidth, int sourceHeight)
    {
        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);

        cropState.Left = Math.Clamp(cropState.Left, 0, sourceWidth - 1);
        cropState.Top = Math.Clamp(cropState.Top, 0, sourceHeight - 1);
        cropState.Width = Math.Clamp(cropState.Width, 1, sourceWidth - cropState.Left);
        cropState.Height = Math.Clamp(cropState.Height, 1, sourceHeight - cropState.Top);
        cropState.ZoomPercentage = Math.Clamp(cropState.ZoomPercentage, 10, 800);
    }

    private static void EnsureRelativeCropStateWithinBounds(RelativeCropState cropState, int sourceWidth, int sourceHeight)
    {
        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);

        cropState.Left = Math.Clamp(cropState.Left, 0, sourceWidth - 1);
        cropState.Top = Math.Clamp(cropState.Top, 0, sourceHeight - 1);
        cropState.ZoomPercentage = Math.Clamp(cropState.ZoomPercentage, 10, 800);
        cropState.Right = Math.Clamp(cropState.Right, 0, Math.Max(0, sourceWidth - cropState.Left - 1));
        cropState.Bottom = Math.Clamp(cropState.Bottom, 0, Math.Max(0, sourceHeight - cropState.Top - 1));
    }

    private static void ApplyAbsoluteCropFieldValue(
        AbsoluteCropState cropState,
        CropField field,
        int value,
        int sourceWidth,
        int sourceHeight)
    {
        switch (field)
        {
            case CropField.Left:
                cropState.Left = value;
                break;
            case CropField.Top:
                cropState.Top = value;
                break;
            case CropField.Width:
                cropState.Width = value;
                break;
            case CropField.Height:
                cropState.Height = value;
                break;
        }

        EnsureAbsoluteCropStateWithinBounds(cropState, sourceWidth, sourceHeight);
    }

    private static void ApplyRelativeCropFieldValue(
        RelativeCropState cropState,
        CropField field,
        int value,
        int sourceWidth,
        int sourceHeight)
    {
        switch (field)
        {
            case CropField.Left:
                cropState.Left = value;
                break;
            case CropField.Top:
                cropState.Top = value;
                break;
            case CropField.Right:
                cropState.Right = value;
                break;
            case CropField.Bottom:
                cropState.Bottom = value;
                break;
        }

        EnsureRelativeCropStateWithinBounds(cropState, sourceWidth, sourceHeight);
    }

    private string BuildCropCommandSnippet(bool useSnippetPlaceholder)
    {
        if (_selectedOutputInfo is null)
        {
            return string.Empty;
        }

        var outputState = GetOrCreateOutputState(_selectedOutputInfo);
        var clipToken = useSnippetPlaceholder ? "${1:clip}" : "clip";
        if (IsAbsoluteCropMode(outputState.CropMode))
        {
            EnsureAbsoluteCropStateWithinBounds(outputState.AbsoluteCrop, _selectedOutputInfo.Width, _selectedOutputInfo.Height);
            return $"{clipToken} = core.std.CropAbs({clipToken}, x={outputState.AbsoluteCrop.Left}, y={outputState.AbsoluteCrop.Top}, width={outputState.AbsoluteCrop.Width}, height={outputState.AbsoluteCrop.Height})";
        }

        EnsureRelativeCropStateWithinBounds(outputState.RelativeCrop, _selectedOutputInfo.Width, _selectedOutputInfo.Height);
        return $"{clipToken} = core.std.CropRel({clipToken}, left={outputState.RelativeCrop.Left}, top={outputState.RelativeCrop.Top}, right={outputState.RelativeCrop.Right}, bottom={outputState.RelativeCrop.Bottom})";
    }

    private static bool IsAbsoluteCropMode(string? mode)
    {
        return string.Equals(mode, "absolute", StringComparison.OrdinalIgnoreCase);
    }

    private static double GetActiveCropZoomPercentage(PreviewOutputState outputState)
    {
        return IsAbsoluteCropMode(outputState.CropMode)
            ? outputState.AbsoluteCrop.ZoomPercentage
            : outputState.RelativeCrop.ZoomPercentage;
    }

    private static void SetActiveCropZoomPercentage(PreviewOutputState outputState, double zoomPercentage)
    {
        if (IsAbsoluteCropMode(outputState.CropMode))
        {
            outputState.AbsoluteCrop.ZoomPercentage = zoomPercentage;
        }
        else
        {
            outputState.RelativeCrop.ZoomPercentage = zoomPercentage;
        }
    }

    private static CropBounds GetEffectiveCropBounds(PreviewOutputState outputState, int sourceWidth, int sourceHeight)
    {
        if (IsAbsoluteCropMode(outputState.CropMode))
        {
            EnsureAbsoluteCropStateWithinBounds(outputState.AbsoluteCrop, sourceWidth, sourceHeight);
            var right = Math.Max(0, sourceWidth - outputState.AbsoluteCrop.Left - outputState.AbsoluteCrop.Width);
            var bottom = Math.Max(0, sourceHeight - outputState.AbsoluteCrop.Top - outputState.AbsoluteCrop.Height);
            return new CropBounds(
                outputState.AbsoluteCrop.Left,
                outputState.AbsoluteCrop.Top,
                outputState.AbsoluteCrop.Width,
                outputState.AbsoluteCrop.Height,
                right,
                bottom);
        }

        EnsureRelativeCropStateWithinBounds(outputState.RelativeCrop, sourceWidth, sourceHeight);
        var width = Math.Max(1, sourceWidth - outputState.RelativeCrop.Left - outputState.RelativeCrop.Right);
        var height = Math.Max(1, sourceHeight - outputState.RelativeCrop.Top - outputState.RelativeCrop.Bottom);
        return new CropBounds(
            outputState.RelativeCrop.Left,
            outputState.RelativeCrop.Top,
            width,
            height,
            outputState.RelativeCrop.Right,
            outputState.RelativeCrop.Bottom);
    }

    private string ResolveSilentSnapshotPath()
    {
        if (_selectedOutputInfo is null || _currentRequest is null)
        {
            throw new InvalidOperationException("Preview context is not ready.");
        }

        var template = string.IsNullOrWhiteSpace(ViewModel.SnapshotTemplate)
            ? "{scriptName}-out{output}-frame{frame}.{ext}"
            : ViewModel.SnapshotTemplate.Trim();
        var scriptName = string.IsNullOrWhiteSpace(_currentRequest.SourceFilePath)
            ? SanitizePathToken(Path.GetFileNameWithoutExtension(_currentRequest.DisplayName))
            : SanitizePathToken(Path.GetFileNameWithoutExtension(_currentRequest.SourceFilePath));
        var timestampText = SanitizePathToken(ViewModel.CurrentTimeText.Replace(":", "-", StringComparison.Ordinal));
        var outputToken = SanitizePathToken(_selectedOutputInfo.Name);
        var result = template
            .Replace("{scriptName}", scriptName, StringComparison.Ordinal)
            .Replace("{output}", string.IsNullOrWhiteSpace(outputToken) ? _selectedOutputInfo.Index.ToString() : outputToken, StringComparison.Ordinal)
            .Replace("{frame}", ViewModel.CurrentFrame.ToString(), StringComparison.Ordinal)
            .Replace("{time}", timestampText, StringComparison.Ordinal)
            .Replace("{ext}", "png", StringComparison.Ordinal);

        if (!Path.IsPathRooted(result))
        {
            var rootDirectory = !string.IsNullOrWhiteSpace(_currentRequest.SourceFilePath)
                ? Path.GetDirectoryName(_currentRequest.SourceFilePath)
                : _currentRequest.WorkingDirectory;
            result = Path.Combine(rootDirectory ?? AppContext.BaseDirectory, result);
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(result)))
        {
            result += ".png";
        }

        return Path.GetFullPath(result);
    }

    private string BuildShortcutSnapshotFileName(bool withExtension)
    {
        if (_selectedOutputInfo is null || _currentRequest is null)
        {
            throw new InvalidOperationException("Preview context is not ready.");
        }

        var scriptFileName = !string.IsNullOrWhiteSpace(_currentRequest.SourceFilePath)
            ? Path.GetFileName(_currentRequest.SourceFilePath)
            : _currentRequest.DisplayName;
        if (string.IsNullOrWhiteSpace(scriptFileName))
        {
            scriptFileName = "preview.vpy";
        }
        else if (string.IsNullOrWhiteSpace(Path.GetExtension(scriptFileName)))
        {
            scriptFileName += ".vpy";
        }

        scriptFileName = SanitizePathToken(scriptFileName);
        var fileName = $"{scriptFileName}-{ViewModel.CurrentFrame}-{_selectedOutputInfo.Index}";
        return withExtension ? $"{fileName}.png" : fileName;
    }

    private string BuildShortcutSnapshotFileName(
        VapourSynthPreviewOutputInfo outputInfo,
        int frameNumber,
        bool withExtension)
    {
        if (_currentRequest is null)
        {
            throw new InvalidOperationException("Preview context is not ready.");
        }

        var scriptFileName = !string.IsNullOrWhiteSpace(_currentRequest.SourceFilePath)
            ? Path.GetFileName(_currentRequest.SourceFilePath)
            : _currentRequest.DisplayName;
        if (string.IsNullOrWhiteSpace(scriptFileName))
        {
            scriptFileName = "preview.vpy";
        }
        else if (string.IsNullOrWhiteSpace(Path.GetExtension(scriptFileName)))
        {
            scriptFileName += ".vpy";
        }

        scriptFileName = SanitizePathToken(scriptFileName);
        var fileName = $"{scriptFileName}-{frameNumber}-{outputInfo.Index}";
        return withExtension ? $"{fileName}.png" : fileName;
    }

    private async Task SaveAllOutputsAtCurrentFrameAsync()
    {
        if (_currentRequest is null || _selectedOutputInfo is null || ViewModel.Outputs.Count == 0)
        {
            return;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        StopPlayback();
        var lockedFrame = ViewModel.CurrentFrame;
        var previousOutput = _selectedOutputInfo;
        var savedCount = 0;
        var outputs = ViewModel.Outputs.Select(static option => option.Info).ToList();

        try
        {
            foreach (var output in outputs)
            {
                var frameNumber = Math.Clamp(lockedFrame, 0, Math.Max(0, output.TotalFrames - 1));
                var snapshotFrame = await RenderSnapshotFrameAsync(output, frameNumber);

                if (snapshotFrame.Pixels.Length == 0 || snapshotFrame.Width <= 0 || snapshotFrame.Height <= 0)
                {
                    continue;
                }

                var snapshotPath = Path.Combine(folder.Path, BuildShortcutSnapshotFileName(output, frameNumber, withExtension: true));
                await SavePixelsAsPngAsync(snapshotPath, snapshotFrame.Pixels, snapshotFrame.Width, snapshotFrame.Height);
                savedCount++;
            }

            await SelectOutputAsync(previousOutput, Math.Clamp(lockedFrame, 0, Math.Max(0, previousOutput.TotalFrames - 1)), useExplicitFrame: true);
            SetStatusText(ViewModel.Texts.VapourSynthPreviewAllSnapshotsSavedStatus(savedCount, outputs.Count, lockedFrame));
        }
        catch (Exception ex)
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewAllSnapshotsFailedStatus(ex.Message));
            if (_selectedOutputInfo?.Index != previousOutput.Index)
            {
                await SelectOutputAsync(previousOutput, Math.Clamp(lockedFrame, 0, Math.Max(0, previousOutput.TotalFrames - 1)), useExplicitFrame: true);
            }
        }
    }

    private async Task<IReadOnlyList<ChapterEntry>> LoadChapterFileAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase))
        {
            return ParseMatroskaChaptersXml(await File.ReadAllTextAsync(filePath));
        }

        return ParseOgmChapters(await File.ReadAllLinesAsync(filePath));
    }

    private async Task<SnapshotFramePayload> RenderSnapshotFrameAsync(
        VapourSynthPreviewOutputInfo outputInfo,
        int frameNumber)
    {
        var frameData = await _previewService.RenderFrameAsync(outputInfo.Index, frameNumber);
        var sourcePixels = await LoadFramePixelsAsync(frameData.RawPixelPath);
        var outputState = GetOrCreateOutputState(outputInfo);
        var cropBounds = GetEffectiveCropBounds(outputState, frameData.Width, frameData.Height);

        if (!ViewModel.IsCropPanelVisible
            || (cropBounds.Left == 0
                && cropBounds.Top == 0
                && cropBounds.Width == frameData.Width
                && cropBounds.Height == frameData.Height))
        {
            return new SnapshotFramePayload(sourcePixels, frameData.Width, frameData.Height);
        }

        return new SnapshotFramePayload(
            CropPixels(
                sourcePixels,
                frameData.Width,
                cropBounds.Left,
                cropBounds.Top,
                cropBounds.Width,
                cropBounds.Height),
            cropBounds.Width,
            cropBounds.Height);
    }

    private IReadOnlyList<VapourSynthPreviewChapterOption> BuildChapterOptions(IEnumerable<ChapterEntry> chapters)
    {
        return chapters
            .OrderBy(static chapter => chapter.Timecode)
            .Select((chapter, index) => new VapourSynthPreviewChapterOption(
                chapter.Timecode,
                chapter.Title,
                ViewModel.FormatChapterLabel(index + 1, chapter.Timecode, chapter.Title)))
            .ToList();
    }

    private List<ChapterEntry> GetChapterEntries()
    {
        return ViewModel.Chapters
            .Select(static chapter => new ChapterEntry(chapter.Timecode, chapter.Title))
            .OrderBy(static chapter => chapter.Timecode)
            .ToList();
    }

    private static IReadOnlyList<ChapterEntry> ParseOgmChapters(IEnumerable<string> lines)
    {
        var timecodes = new Dictionary<int, TimeSpan>();
        var titles = new Dictionary<int, string>();
        var pattern = new Regex(@"^CHAPTER(?<index>\d+)(?<name>NAME)?=(?<value>.*)$", RegexOptions.IgnoreCase);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var match = pattern.Match(line);
            if (!match.Success
                || !int.TryParse(match.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var value = match.Groups["value"].Value.Trim();
            if (match.Groups["name"].Success)
            {
                titles[index] = StripOgmLanguagePrefix(value);
            }
            else if (TryParseChapterTimecode(value, out var timecode))
            {
                timecodes[index] = timecode;
            }
        }

        return timecodes
            .OrderBy(static item => item.Key)
            .Select(item => new ChapterEntry(
                item.Value,
                titles.TryGetValue(item.Key, out var title) && !string.IsNullOrWhiteSpace(title)
                    ? title
                    : $"Chapter {item.Key:00}"))
            .ToList();
    }

    private static IReadOnlyList<ChapterEntry> ParseMatroskaChaptersXml(string xml)
    {
        var document = XDocument.Parse(xml);
        return document
            .Descendants("ChapterAtom")
            .Select(atom =>
            {
                var startText = atom.Element("ChapterTimeStart")?.Value.Trim();
                var title = atom
                    .Elements("ChapterDisplay")
                    .Elements("ChapterString")
                    .FirstOrDefault()
                    ?.Value
                    .Trim();

                return TryParseChapterTimecode(startText, out var timecode)
                    ? new ChapterEntry(timecode, string.IsNullOrWhiteSpace(title) ? "Chapter" : title)
                    : null;
            })
            .Where(static chapter => chapter is not null)
            .Select(static chapter => chapter!)
            .OrderBy(static chapter => chapter.Timecode)
            .ToList();
    }

    private static IEnumerable<string> BuildOgmChapterLines(IReadOnlyList<ChapterEntry> chapters)
    {
        for (var index = 0; index < chapters.Count; index++)
        {
            var chapterNumber = index + 1;
            yield return $"CHAPTER{chapterNumber:00}={FormatChapterTimecode(chapters[index].Timecode)}";
            yield return $"CHAPTER{chapterNumber:00}NAME={chapters[index].Title}";
        }
    }

    private static string BuildMatroskaChaptersXml(IReadOnlyList<ChapterEntry> chapters)
    {
        var editionEntry = new XElement("EditionEntry",
            chapters.Select((chapter, index) =>
                new XElement("ChapterAtom",
                    new XElement("ChapterTimeStart", FormatChapterTimecode(chapter.Timecode)),
                    new XElement("ChapterUID", index + 1),
                    new XElement("ChapterDisplay",
                        new XElement("ChapterString", chapter.Title),
                        new XElement("ChapterLanguage", "und")))));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("Chapters", editionEntry));
        return document.ToString();
    }

    private static bool TryParseChapterTimecode(string? value, out TimeSpan timecode)
    {
        timecode = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim().Replace(',', '.');
        var parts = text.Split(':');
        if (parts.Length == 2)
        {
            text = $"00:{text}";
        }

        return TimeSpan.TryParseExact(
                text,
                [@"h\:mm\:ss\.fff", @"hh\:mm\:ss\.fff", @"h\:mm\:ss", @"hh\:mm\:ss"],
                CultureInfo.InvariantCulture,
                out timecode)
            || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out timecode);
    }

    private static string FormatChapterTimecode(TimeSpan timecode)
    {
        return timecode.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    private static string StripOgmLanguagePrefix(string value)
    {
        var separatorIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex > 0 && separatorIndex <= 3)
        {
            return value[(separatorIndex + 1)..].Trim();
        }

        return value.Trim();
    }

    private async Task<ChapterEntry?> ShowChapterEditDialogAsync(TimeSpan timecode, string title)
    {
        var titleBox = new TextBox
        {
            Header = ViewModel.Texts.VapourSynthPreviewChapterTitleHeader,
            Text = title
        };
        var timecodeBox = new TextBox
        {
            Header = ViewModel.Texts.VapourSynthPreviewChapterTimecodeHeader,
            Text = FormatChapterTimecode(timecode)
        };
        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                titleBox,
                timecodeBox
            }
        };

        var dialog = new ContentDialog
        {
            Title = ViewModel.Texts.VapourSynthPreviewChapterDialogTitle,
            PrimaryButtonText = ViewModel.Texts.SaveButton,
            CloseButtonText = ViewModel.Texts.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Content = panel
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        if (!TryParseChapterTimecode(timecodeBox.Text, out var parsedTimecode))
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewChapterTimecodeInvalidStatus);
            return null;
        }

        var normalizedTitle = titleBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            normalizedTitle = ViewModel.Texts.VapourSynthPreviewChapterFallbackTitle(ViewModel.Chapters.Count + 1);
        }

        return new ChapterEntry(parsedTimecode, normalizedTitle);
    }

    private void UpdateChapterButtons()
    {
        var hasChapters = ViewModel.Chapters.Count > 0;
        ExportChapterButton.IsEnabled = hasChapters;
        EditChapterButton.IsEnabled = hasChapters;
        DeleteChapterButton.IsEnabled = hasChapters;
        NextChapterButton.IsEnabled = hasChapters;
        ViewModel.NotifyChaptersChanged();
    }

    private int ResolveActiveChapterIndex()
    {
        if (ViewModel.Chapters.Count == 0)
        {
            return -1;
        }

        if (_activeChapterIndex >= 0 && _activeChapterIndex < ViewModel.Chapters.Count)
        {
            return _activeChapterIndex;
        }

        if (_selectedOutputInfo is null)
        {
            return 0;
        }

        var currentFrame = ViewModel.CurrentFrame;
        var nearest = ViewModel.Chapters
            .Select((chapter, index) => new
            {
                Index = index,
                Frame = TimecodeToFrame(chapter.Timecode, _selectedOutputInfo)
            })
            .OrderBy(item => Math.Abs(item.Frame - currentFrame))
            .First();

        _activeChapterIndex = nearest.Index;
        return nearest.Index;
    }

    private void UpdateActiveChapter(int frameNumber)
    {
        if (_selectedOutputInfo is null || ViewModel.Chapters.Count == 0)
        {
            _activeChapterIndex = -1;
            UpdateChapterButtons();
            return;
        }

        var nearest = ViewModel.Chapters
            .Select((chapter, index) => new
            {
                Index = index,
                Frame = TimecodeToFrame(chapter.Timecode, _selectedOutputInfo)
            })
            .OrderBy(item => Math.Abs(item.Frame - frameNumber))
            .First();
        var toleranceFrames = Math.Max(1, (int)Math.Round(GetOutputFps(_selectedOutputInfo) * 0.1));
        _activeChapterIndex = Math.Abs(nearest.Frame - frameNumber) <= toleranceFrames
            ? nearest.Index
            : -1;
        UpdateChapterButtons();
    }

    private void RedrawChapterMarkers()
    {
        ChapterMarkerCanvas.Children.Clear();
        if (_selectedOutputInfo is null || ViewModel.Chapters.Count == 0)
        {
            return;
        }

        var height = ChapterMarkerCanvas.ActualHeight;
        if (height <= 0)
        {
            height = FrameSlider.ActualHeight;
        }

        if (height <= 0 || FrameSlider.ActualWidth <= 0)
        {
            return;
        }

        var track = MeasureSliderTrackBounds();
        if (track.width <= 0)
        {
            return;
        }

        var maxFrame = Math.Max(1, ViewModel.FrameSliderMaximum);
        for (var index = 0; index < ViewModel.Chapters.Count; index++)
        {
            var frame = TimecodeToFrame(ViewModel.Chapters[index].Timecode, _selectedOutputInfo);
            var x = track.offset + (frame / maxFrame) * track.width;
            var marker = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = 3,
                Y2 = Math.Max(4, height - 3),
                StrokeThickness = index == _activeChapterIndex ? 3 : 2,
                Stroke = new SolidColorBrush(index == _activeChapterIndex
                    ? Microsoft.UI.Colors.Orange
                    : Microsoft.UI.Colors.Gold)
            };
            ChapterMarkerCanvas.Children.Add(marker);
        }
    }

    private (double offset, double width) MeasureSliderTrackBounds()
    {
        var thumbWidth = 24.0;
        try
        {
            var thumb = FindDescendant<Thumb>(FrameSlider);
            if (thumb is not null && thumb.ActualWidth > 0)
            {
                thumbWidth = thumb.ActualWidth;
            }
        }
        catch
        {
        }

        return (thumbWidth / 2.0, Math.Max(1, FrameSlider.ActualWidth - thumbWidth));
    }

    private static string SanitizePathToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "preview";
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }

    private static async Task SavePixelsAsPngAsync(string filePath, byte[] pixels, int width, int height)
    {
        using var outputStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None).AsRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96,
            96,
            pixels);
        await encoder.FlushAsync();
    }

    private async Task ShowAdvancedSettingsAsync()
    {
        var outputSyncComboBox = new ComboBox
        {
            DisplayMemberPath = nameof(StringChoiceOption.Label),
            ItemsSource = ViewModel.OutputSyncModes,
            SelectedItem = ViewModel.SelectedOutputSyncMode
        };
        var silentSnapshotToggle = new ToggleSwitch
        {
            Header = ViewModel.Texts.VapourSynthPreviewSilentSnapshotHeader,
            IsOn = ViewModel.SilentSnapshotEnabled,
            OnContent = ViewModel.Texts.ToggleOnLabel,
            OffContent = ViewModel.Texts.ToggleOffLabel
        };
        var snapshotTemplateTextBox = new TextBox
        {
            Header = ViewModel.Texts.VapourSynthPreviewSnapshotTemplateHeader,
            Text = ViewModel.SnapshotTemplate,
            IsEnabled = ViewModel.SilentSnapshotEnabled
        };

        silentSnapshotToggle.Toggled += (_, _) =>
        {
            snapshotTemplateTextBox.IsEnabled = silentSnapshotToggle.IsOn;
        };

        var dialog = new ContentDialog
        {
            Title = ViewModel.Texts.VapourSynthPreviewAdvancedSettingsTitle,
            PrimaryButtonText = ViewModel.Texts.SaveButton,
            CloseButtonText = ViewModel.Texts.CancelButton,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootLayout.XamlRoot,
            RequestedTheme = RootLayout.ActualTheme,
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = ViewModel.Texts.VapourSynthPreviewOutputSyncHeader
                    },
                    outputSyncComboBox,
                    silentSnapshotToggle,
                    snapshotTemplateTextBox,
                    new TextBlock
                    {
                        Text = ViewModel.Texts.VapourSynthPreviewSnapshotTemplateHint,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        ViewModel.SelectedOutputSyncMode = outputSyncComboBox.SelectedItem as StringChoiceOption
            ?? ViewModel.OutputSyncModes.FirstOrDefault();
        ViewModel.SilentSnapshotEnabled = silentSnapshotToggle.IsOn;
        ViewModel.SnapshotTemplate = snapshotTemplateTextBox.Text?.Trim() ?? string.Empty;
        SetStatusText(ViewModel.Texts.VapourSynthPreviewAdvancedSettingsSavedStatus);
    }

    private void StopPlayback(bool updateStatus = false)
    {
        _playbackTimer.Stop();
        _isPlaying = false;
        _pendingFrameNumber = null;
        PlayPauseIcon.Symbol = Symbol.Play;

        if (updateStatus && _selectedOutputInfo is not null && _lastFrameData is not null)
        {
            SetStatusText(ViewModel.Texts.VapourSynthPreviewReadyStatus(_selectedOutputInfo.Index, ViewModel.CurrentFrame));
        }
    }

    private void SetStatusText(string statusText)
    {
        _pendingStatusText = null;
        _statusCommitTimer.Stop();
        ViewModel.UpdateStatus(statusText);
    }

    private void QueueFrameStatusText(string statusText)
    {
        _pendingStatusText = statusText;
        _statusCommitTimer.Stop();
        _statusCommitTimer.Start();
    }

    private void StatusCommitTimer_Tick(object? sender, object e)
    {
        _statusCommitTimer.Stop();
        if (string.IsNullOrWhiteSpace(_pendingStatusText))
        {
            return;
        }

        ViewModel.UpdateStatus(_pendingStatusText);
        _pendingStatusText = null;
    }

    private void DetachControlEvents()
    {
        OutputSelectorComboBox.SelectionChanged -= OutputSelectorComboBox_SelectionChanged;
        FrameNumberBox.ValueChanged -= FrameNumberBox_ValueChanged;
        ZoomModeComboBox.SelectionChanged -= ZoomModeComboBox_SelectionChanged;
        ZoomRatioBox.ValueChanged -= ZoomRatioBox_ValueChanged;
        TimelineModeComboBox.SelectionChanged -= TimelineModeComboBox_SelectionChanged;
        TimeStepSecondsBox.ValueChanged -= TimeStepSecondsBox_ValueChanged;
        CropModeComboBox.SelectionChanged -= CropModeComboBox_SelectionChanged;
        CropLeftBox.ValueChanged -= CropLeftBox_ValueChanged;
        CropTopBox.ValueChanged -= CropTopBox_ValueChanged;
        CropWidthBox.ValueChanged -= CropWidthBox_ValueChanged;
        CropHeightBox.ValueChanged -= CropHeightBox_ValueChanged;
        CropRightBox.ValueChanged -= CropRightBox_ValueChanged;
        CropBottomBox.ValueChanged -= CropBottomBox_ValueChanged;
        CropZoomBox.ValueChanged -= CropZoomBox_ValueChanged;
        StepSizeBox.ValueChanged -= StepSizeBox_ValueChanged;
        FrameSlider.ValueChanged -= FrameSlider_ValueChanged;
        ChapterSelectorComboBox.SelectionChanged -= ChapterSelectorComboBox_SelectionChanged;
    }

    private void AttachControlEvents()
    {
        OutputSelectorComboBox.SelectionChanged -= OutputSelectorComboBox_SelectionChanged;
        FrameNumberBox.ValueChanged -= FrameNumberBox_ValueChanged;
        ZoomModeComboBox.SelectionChanged -= ZoomModeComboBox_SelectionChanged;
        ZoomRatioBox.ValueChanged -= ZoomRatioBox_ValueChanged;
        TimelineModeComboBox.SelectionChanged -= TimelineModeComboBox_SelectionChanged;
        TimeStepSecondsBox.ValueChanged -= TimeStepSecondsBox_ValueChanged;
        CropModeComboBox.SelectionChanged -= CropModeComboBox_SelectionChanged;
        CropLeftBox.ValueChanged -= CropLeftBox_ValueChanged;
        CropTopBox.ValueChanged -= CropTopBox_ValueChanged;
        CropWidthBox.ValueChanged -= CropWidthBox_ValueChanged;
        CropHeightBox.ValueChanged -= CropHeightBox_ValueChanged;
        CropRightBox.ValueChanged -= CropRightBox_ValueChanged;
        CropBottomBox.ValueChanged -= CropBottomBox_ValueChanged;
        CropZoomBox.ValueChanged -= CropZoomBox_ValueChanged;
        StepSizeBox.ValueChanged -= StepSizeBox_ValueChanged;
        FrameSlider.ValueChanged -= FrameSlider_ValueChanged;

        OutputSelectorComboBox.SelectionChanged += OutputSelectorComboBox_SelectionChanged;
        FrameNumberBox.ValueChanged += FrameNumberBox_ValueChanged;
        ZoomModeComboBox.SelectionChanged += ZoomModeComboBox_SelectionChanged;
        ZoomRatioBox.ValueChanged += ZoomRatioBox_ValueChanged;
        TimelineModeComboBox.SelectionChanged += TimelineModeComboBox_SelectionChanged;
        TimeStepSecondsBox.ValueChanged += TimeStepSecondsBox_ValueChanged;
        CropModeComboBox.SelectionChanged += CropModeComboBox_SelectionChanged;
        CropLeftBox.ValueChanged += CropLeftBox_ValueChanged;
        CropTopBox.ValueChanged += CropTopBox_ValueChanged;
        CropWidthBox.ValueChanged += CropWidthBox_ValueChanged;
        CropHeightBox.ValueChanged += CropHeightBox_ValueChanged;
        CropRightBox.ValueChanged += CropRightBox_ValueChanged;
        CropBottomBox.ValueChanged += CropBottomBox_ValueChanged;
        CropZoomBox.ValueChanged += CropZoomBox_ValueChanged;
        StepSizeBox.ValueChanged += StepSizeBox_ValueChanged;
        FrameSlider.ValueChanged += FrameSlider_ValueChanged;
        ChapterSelectorComboBox.SelectionChanged += ChapterSelectorComboBox_SelectionChanged;
    }

    private nint GetWindowHandle()
    {
        return WindowNative.GetWindowHandle(this);
    }

    private void EnsurePreferredWindowPresentation()
    {
        if (TryEnterFullScreen())
        {
            return;
        }

        if (TryMaximizeWindow())
        {
            return;
        }

        TryResizeWindow();
    }

    private bool TryRestoreWindowedPresentation()
    {
        if (TryMaximizeWindow())
        {
            return true;
        }

        return TryResizeWindow();
    }

    private bool TryEnterFullScreen()
    {
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            _isFullScreenActive = true;
            return true;
        }
        catch
        {
            _isFullScreenActive = false;
            return false;
        }
    }

    private bool TryMaximizeWindow()
    {
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
                _isFullScreenActive = false;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private bool TryResizeWindow()
    {
        try
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            AppWindow.Resize(new SizeInt32(1560, 1040));
            _isFullScreenActive = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] CropPixels(byte[] sourcePixels, int sourceWidth, int left, int top, int width, int height)
    {
        var targetPixels = new byte[width * height * 4];
        var bytesPerPixel = 4;
        var sourceStride = sourceWidth * bytesPerPixel;
        var targetStride = width * bytesPerPixel;

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = ((top + row) * sourceWidth + left) * bytesPerPixel;
            var targetOffset = row * targetStride;
            System.Buffer.BlockCopy(sourcePixels, sourceOffset, targetPixels, targetOffset, targetStride);
        }

        return targetPixels;
    }

    private static int GetIntValue(NumberBox numberBox)
    {
        return double.IsNaN(numberBox.Value) ? 0 : (int)Math.Round(numberBox.Value);
    }

    private bool CanPanPreview()
    {
        return ViewModel.PreviewImageWidth > PreviewViewportHost.ActualWidth + 1
            || ViewModel.PreviewImageHeight > PreviewViewportHost.ActualHeight + 1;
    }

    private double GetCurrentEffectiveZoomRatio()
    {
        if (_displayedFrameWidth <= 0 || ViewModel.PreviewImageWidth <= 0)
        {
            return Math.Clamp(ViewModel.ZoomRatio, 0.05, 16.0);
        }

        var displayScale = RootLayout.XamlRoot?.RasterizationScale ?? 1.0;
        var visibleRatio = ViewModel.PreviewImageWidth / _displayedFrameWidth;
        var cropMultiplier = ViewModel.IsCropPanelVisible
            ? Math.Max(0.1, ViewModel.CropZoomPercentage / 100.0)
            : 1.0;

        var zoomRatio = visibleRatio * displayScale / cropMultiplier;
        if (zoomRatio <= 0 || double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio))
        {
            return Math.Clamp(ViewModel.ZoomRatio, 0.05, 16.0);
        }

        return Math.Clamp(zoomRatio, 0.05, 16.0);
    }

    private void ApplyCustomZoom(double zoomRatio)
    {
        var customZoomOption = ViewModel.ZoomModes.FirstOrDefault(option => option.Value == "custom");
        ViewModel.ZoomRatio = zoomRatio;
        if (customZoomOption is not null)
        {
            ViewModel.SelectedZoomMode = customZoomOption;
        }

        SaveCurrentOutputState();
        SyncControls();
    }

    private void RestorePreviewScrollAnchor(
        double anchorXRatio,
        double anchorYRatio,
        double pointerViewportX,
        double pointerViewportY)
    {
        PreviewScrollViewer.UpdateLayout();

        var targetHorizontalOffset = Math.Clamp(
            ViewModel.PreviewImageWidth * anchorXRatio - pointerViewportX,
            0,
            PreviewScrollViewer.ScrollableWidth);
        var targetVerticalOffset = Math.Clamp(
            ViewModel.PreviewImageHeight * anchorYRatio - pointerViewportY,
            0,
            PreviewScrollViewer.ScrollableHeight);

        PreviewScrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null, true);
    }

    private void ReleasePreviewPanCapture()
    {
        _isPreviewPanActive = false;
        _previewPanPointerId = 0;
        PreviewScrollViewer.ReleasePointerCaptures();
    }

    private static bool IsControlKeyPressed()
    {
        return Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private void RootLayout_Loaded(object sender, RoutedEventArgs e)
    {
        AttachXamlRoot();
        RefreshDisplayScale();
        QueueAttachPreviewNumberBoxEditorHandlers();
    }

    private void RootLayout_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachXamlRoot();
        DetachPreviewNumberBoxEditorHandlers();
    }

    private void RootLayout_ActualThemeChanged(FrameworkElement sender, object args)
    {
        QueueAttachPreviewNumberBoxEditorHandlers();
    }

    private void AttachXamlRoot()
    {
        if (ReferenceEquals(_observedXamlRoot, RootLayout.XamlRoot))
        {
            return;
        }

        DetachXamlRoot();
        _observedXamlRoot = RootLayout.XamlRoot;
        if (_observedXamlRoot is not null)
        {
            _observedXamlRoot.Changed += ObservedXamlRoot_Changed;
        }
    }

    private void DetachXamlRoot()
    {
        if (_observedXamlRoot is null)
        {
            return;
        }

        _observedXamlRoot.Changed -= ObservedXamlRoot_Changed;
        _observedXamlRoot = null;
    }

    private void ObservedXamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        RefreshDisplayScale();
    }

    private void RefreshDisplayScale()
    {
        AttachXamlRoot();
        ViewModel.UpdateDisplayScale(RootLayout.XamlRoot?.RasterizationScale ?? 1.0);
    }

    private async void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (e.Key == VirtualKey.F11)
        {
            e.Handled = _isFullScreenActive
                ? TryRestoreWindowedPresentation()
                : TryEnterFullScreen();
            return;
        }

        if (e.Key == VirtualKey.Escape && _isFullScreenActive)
        {
            e.Handled = TryRestoreWindowedPresentation();
            return;
        }

        if (IsEditingShortcutSuppressed())
        {
            return;
        }

        if (TryGetOutputIndexFromKey(e.Key, out var outputIndex))
        {
            var target = ViewModel.Outputs.FirstOrDefault(option => option.Info.Index == outputIndex);
            if (target is not null)
            {
                e.Handled = true;
                await SelectOutputAsync(target.Info);
            }

            return;
        }

        if (TryGetFrameNavigationDelta(e.Key, out var frameDelta))
        {
            e.Handled = true;
            await RenderFrameAsync(ViewModel.CurrentFrame + frameDelta);
            return;
        }

        if (e.Key == VirtualKey.S)
        {
            e.Handled = true;
            if (IsControlKeyPressed())
            {
                await SaveAllOutputsAtCurrentFrameAsync();
            }
            else
            {
                await QuickSaveSnapshotAsync();
            }
        }
    }

    private void RootLayout_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (TryFindAncestor<NumberBox>(e.OriginalSource as DependencyObject) is null)
        {
            return;
        }

        e.Handled = true;
        FocusPreviewSurface();
        QueueFocusPreviewSurface();
    }

    private void NumberBoxEditor_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.Key != VirtualKey.Enter)
        {
            return;
        }

        RootLayout.DispatcherQueue.TryEnqueue(() =>
        {
            _ = FocusPreviewSurface();
            QueueFocusPreviewSurface();
        });
    }

    private bool IsEditingShortcutSuppressed()
    {
        var focusedElement = FocusManager.GetFocusedElement(RootLayout.XamlRoot);
        return focusedElement is TextBox
            or PasswordBox
            or RichEditBox
            or ComboBox
            or NumberBox;
    }

    private bool FocusPreviewSurface()
    {
        if (_isClosed)
        {
            return false;
        }

        if (PreviewScrollViewer.Focus(FocusState.Programmatic))
        {
            return true;
        }

        return ReloadPreviewButton.Focus(FocusState.Programmatic);
    }

    private void QueueFocusPreviewSurface()
    {
        if (_isClosed)
        {
            return;
        }

        RootLayout.DispatcherQueue.TryEnqueue(() => _ = FocusPreviewSurface());
    }

    private static TControl? TryFindAncestor<TControl>(DependencyObject? source)
        where TControl : DependencyObject
    {
        while (source is not null)
        {
            if (source is TControl control)
            {
                return control;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void AttachPreviewNumberBoxEditorHandlers()
    {
        DetachPreviewNumberBoxEditorHandlers();

        foreach (var numberBox in EnumeratePreviewNumberBoxes())
        {
            numberBox.ApplyTemplate();

            var editor = FindDescendant<TextBox>(numberBox);
            if (editor is null)
            {
                continue;
            }

            editor.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(NumberBoxEditor_KeyDown), true);
            _attachedPreviewNumberBoxEditors.Add(editor);
        }
    }

    private void DetachPreviewNumberBoxEditorHandlers()
    {
        foreach (var editor in _attachedPreviewNumberBoxEditors)
        {
            editor.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(NumberBoxEditor_KeyDown));
        }

        _attachedPreviewNumberBoxEditors.Clear();
    }

    private void QueueAttachPreviewNumberBoxEditorHandlers()
    {
        if (_isClosed)
        {
            return;
        }

        RootLayout.DispatcherQueue.TryEnqueue(AttachPreviewNumberBoxEditorHandlers);
    }

    private IEnumerable<NumberBox> EnumeratePreviewNumberBoxes()
    {
        yield return FrameNumberBox;
        yield return ZoomRatioBox;
        yield return StepSizeBox;
        yield return TimeStepSecondsBox;
        yield return CropLeftBox;
        yield return CropTopBox;
        yield return CropWidthBox;
        yield return CropHeightBox;
        yield return CropRightBox;
        yield return CropBottomBox;
        yield return CropZoomBox;
    }

    private static TElement? FindDescendant<TElement>(DependencyObject root)
        where TElement : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TElement element)
            {
                return element;
            }

            var descendant = FindDescendant<TElement>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static bool TryGetFrameNavigationDelta(VirtualKey key, out int frameDelta)
    {
        frameDelta = key switch
        {
            VirtualKey.Left or VirtualKey.Up => -1,
            VirtualKey.Right or VirtualKey.Down => 1,
            _ => 0
        };

        return frameDelta != 0;
    }

    private static bool TryGetOutputIndexFromKey(VirtualKey key, out int outputIndex)
    {
        outputIndex = key switch
        {
            VirtualKey.Number0 or VirtualKey.NumberPad0 => 0,
            VirtualKey.Number1 or VirtualKey.NumberPad1 => 1,
            VirtualKey.Number2 or VirtualKey.NumberPad2 => 2,
            VirtualKey.Number3 or VirtualKey.NumberPad3 => 3,
            VirtualKey.Number4 or VirtualKey.NumberPad4 => 4,
            VirtualKey.Number5 or VirtualKey.NumberPad5 => 5,
            VirtualKey.Number6 or VirtualKey.NumberPad6 => 6,
            VirtualKey.Number7 or VirtualKey.NumberPad7 => 7,
            VirtualKey.Number8 or VirtualKey.NumberPad8 => 8,
            VirtualKey.Number9 or VirtualKey.NumberPad9 => 9,
            _ => -1
        };

        return outputIndex >= 0;
    }

    private async Task ScrollToCropBoundaryAsync(CropField? field)
    {
        if (!ViewModel.IsCropPanelVisible)
        {
            return;
        }

        await Task.Yield();
        PreviewScrollViewer.UpdateLayout();

        double? horizontalOffset = null;
        double? verticalOffset = null;

        switch (field)
        {
            case null:
            case CropField.Left:
                horizontalOffset = 0;
                break;
            case CropField.Right:
            case CropField.Width:
                horizontalOffset = PreviewScrollViewer.ScrollableWidth;
                break;
        }

        switch (field)
        {
            case null:
            case CropField.Top:
                verticalOffset = 0;
                break;
            case CropField.Bottom:
            case CropField.Height:
                verticalOffset = PreviewScrollViewer.ScrollableHeight;
                break;
        }

        PreviewScrollViewer.ChangeView(horizontalOffset, verticalOffset, null, true);
    }

    private sealed record DisplayFramePayload(
        WriteableBitmap Bitmap,
        byte[] Pixels,
        int Width,
        int Height,
        string ResolutionText);

    private sealed record SnapshotFramePayload(
        byte[] Pixels,
        int Width,
        int Height);

    private sealed record ChapterEntry(
        TimeSpan Timecode,
        string Title);

    private sealed class PreviewOutputState
    {
        public bool HasVisited { get; set; }

        public int CurrentFrame { get; set; }

        public string ZoomMode { get; set; } = "actual";

        public double ZoomRatio { get; set; } = 1.0;

        public string CropMode { get; set; } = "relative";

        public AbsoluteCropState AbsoluteCrop { get; init; } = new();

        public RelativeCropState RelativeCrop { get; init; } = new();

        public static PreviewOutputState CreateDefault(int width, int height)
        {
            return new PreviewOutputState
            {
                AbsoluteCrop = AbsoluteCropState.CreateDefault(width, height),
                RelativeCrop = RelativeCropState.CreateDefault()
            };
        }
    }

    private sealed record PreviewZoomState(
        string ZoomMode,
        double ZoomRatio);

    private sealed class AbsoluteCropState
    {
        public int Left { get; set; }

        public int Top { get; set; }

        public int Width { get; set; } = 1;

        public int Height { get; set; } = 1;

        public double ZoomPercentage { get; set; } = 100;

        public static AbsoluteCropState CreateDefault(int width, int height)
        {
            return new AbsoluteCropState
            {
                Width = Math.Max(1, width),
                Height = Math.Max(1, height),
                ZoomPercentage = 200
            };
        }
    }

    private sealed class RelativeCropState
    {
        public int Left { get; set; }

        public int Top { get; set; }

        public int Right { get; set; }

        public int Bottom { get; set; }

        public double ZoomPercentage { get; set; } = 100;

        public static RelativeCropState CreateDefault()
        {
            return new RelativeCropState
            {
                ZoomPercentage = 200
            };
        }
    }

    private sealed record CropBounds(
        int Left,
        int Top,
        int Width,
        int Height,
        int Right,
        int Bottom);

    private enum CropField
    {
        Left,
        Top,
        Width,
        Height,
        Right,
        Bottom
    }
}
