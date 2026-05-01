using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FlowEncode.Application;
using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FlowEncode.ViewModels;

public partial class MainWindowViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private const string AppReleasePageUrl = "https://github.com/frankie1024/FlowEncode/releases";
    private const int MinConcurrentEncodingJobs = 1;
    private const int MaxConcurrentEncodingJobsLimit = 5;
    private static readonly TimeSpan InputPathRefreshDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan QueueCompletionActionIdleRequirement = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan QueueCompletionActionIdlePollInterval = TimeSpan.FromSeconds(5);
    private readonly IEncoderToolchainService _toolchainService;
    private readonly IProfileLibraryService _profileLibraryService;
    private readonly IEncodingJobRunner _jobRunner;
    private readonly IQueueCompletionActionService _queueCompletionActionService;
    private readonly ISystemIdleService _systemIdleService;
    private readonly IAutoCompressionRunner _autoCompressionRunner;
    private readonly IAudioProcessingRunner _audioProcessingRunner;
    private readonly IAudioSourceInfoService _audioSourceInfoService;
    private readonly IBluRayDiscProbeService _bluRayDiscProbeService;
    private readonly IBluRayDemuxRunner _bluRayDemuxRunner;
    private readonly IAppSettingsService _settingsService;
    private readonly ISetupGuideCacheService _setupGuideCacheService;
    private readonly IToolRegistryService _toolRegistryService;
    private readonly IToolProbeService _toolProbeService;
    private readonly IEncoderDiscoveryService _encoderDiscoveryService;
    private readonly IEnvironmentReadinessService _environmentReadinessService;
    private readonly ISetupBootstrapService _setupBootstrapService;
    private readonly IAppUpdateService _appUpdateService;

    private EncodingProfile? _activeProfile;
    private EnvironmentReadinessReport? _environmentReadinessReport;
    private bool _isShuttingDown;
    private bool _isRefreshingCatalog;
    private bool _isCheckingUpdates;
    private bool _isDownloadingAppUpdateInstaller;
    private int? _appUpdateDownloadProgressPercent;
    private bool _isSetupGuideInstallRunning;
    private bool _isRefreshingSetupGuide;
    private bool _isCheckingSetupDependencyUpdates;
    private DateTimeOffset? _setupGuideLocalCheckedAt;
    private DateTimeOffset? _setupGuideRemoteCheckedAt;
    private string _statusText = "环境已准备完成，等待首次刷新。";
    private string _previewTitle = "选择一个预设以生成命令预览";
    private string _previewCommandLine = string.Empty;
    private string _previewNotes = "预览命令会围绕后续的作业队列和滤镜管线展开。";
    private string _selectedProfileCaption = "尚未选择预设";
    private string _draftTemplateName = string.Empty;
    private string _draftTemplateNotes = string.Empty;
    private string _templateSearchText = string.Empty;
    private string? _editingTemplateId;
    private string? _currentTemplateSelectionKey;
    private string _templateBaselineName = string.Empty;
    private string _templateBaselineNotes = string.Empty;
    private EncodingProfile? _templateBaselineProfile;
    private string _sourcePath = string.Empty;
    private string _outputPath = string.Empty;
    private AppText _texts = new(AppLanguage.Chinese);
    private ThemeOption? _selectedTheme;
    private LanguageOption? _selectedLanguage;
    private EncoderOption? _selectedEncoder;
    private RateControlOption? _selectedRateControl;
    private StringChoiceOption? _selectedPreset;
    private StringChoiceOption? _selectedTune;
    private StringChoiceOption? _selectedProfileOption;
    private StringChoiceOption? _selectedOutputFormat;
    private StringChoiceOption? _selectedConcurrentEncodingJobOption;
    private StringChoiceOption? _selectedQueueCompletionActionOption;
    private bool _preferSystemEncoders;
    private bool _autoCheckUpdatesOnStartup;
    private double _maxConcurrentEncodingJobs = MinConcurrentEncodingJobs;
    private QueueCompletionAction _queueCompletionAction = QueueCompletionAction.None;
    private IReadOnlyDictionary<string, string> _manualToolPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private bool _hasRunInitialVsPluginDependencyUpdate;
    private string _workspaceRootPath = string.Empty;
    private AppUpdateCheckResult? _lastAppUpdateResult;
    private string? _lastAppUpdateErrorMessage;
    private EncodingJobItemViewModel? _selectedJob;
    private readonly List<EncodingJobItemViewModel> _selectedQueueJobs = [];
    private string _draftAdditionalArguments = string.Empty;
    private string _draftUhdParameters = string.Empty;
    private double _draftQuality = 18.0;
    private double _draftBitrate = 3500.0;
    private string _draftProfileName = "x264 草稿";
    private string _draftProfileDescription = "先选择输入源和编码器，再微调当前作业的编码参数。";
    private string? _lastAutoOutputPath;
    private bool _isSynchronizingDraft;
    private bool _isUpdatingOutputPath;
    private bool _isQueueProcessing;
    private bool _isQueueCompletionActionArmed;
    private bool _queueCompletionActionBatchHadNonSuccessfulCompletion;
    private bool _isExecutingQueueCompletionAction;
    private CancellationTokenSource? _queueCompletionActionWaitCancellationTokenSource;
    private string _autoCompressionSourcePath = string.Empty;
    private string _autoCompressionOutputPath = string.Empty;
    private string _autoCompressionVideoParameters = string.Empty;
    private double _autoCompressionTargetVmaf = 95.0;
    private double _autoCompressionProbes = 4;
    private double _autoCompressionWorkers;
    private EncoderOption? _selectedAutoEncoder;
    private string _autoCompressionStatusText = string.Empty;
    private string _autoCompressionCommandLine = string.Empty;
    private string _autoCompressionLog = string.Empty;
    private double _autoCompressionProgressPercent;
    private bool _autoCompressionProgressIsIndeterminate;
    private bool _isAutoCompressionRunning;
    private string? _lastAutoCompressionOutputPath;
    private bool _isUpdatingAutoCompressionOutputPath;
    private CancellationTokenSource? _autoCompressionCancellationTokenSource;
    private Guid? _activeAutoCompressionJobId;
    private EncodingJobState? _autoCompressionDisplayState;
    private readonly StringBuilder _autoCompressionLogBuilder = new();
    private readonly List<string> _autoCompressionLogStageLines = [];
    private string _autoCompressionLiveLogLine = string.Empty;
    private bool _isDisposed;
    private CancellationTokenSource? _previewRefreshCancellationTokenSource;
    private CancellationTokenSource? _draftInputRefreshCancellationTokenSource;
    private CancellationTokenSource? _autoCompressionInputRefreshCancellationTokenSource;
    private int _previewRefreshVersion;
    private int _draftInputRefreshVersion;
    private int _autoCompressionInputRefreshVersion;
    private bool _isApplyingDeferredDraftInputRefresh;
    private bool _isApplyingDeferredAutoCompressionInputRefresh;
    private bool _isDraftInputRefreshPending;
    private bool _isAutoCompressionInputRefreshPending;
    private const int AutoCompressionLogLimit = 120_000;
    private const int AutoCompressionStageLogLimit = 240;

    public MainWindowViewModel(
        IEncoderToolchainService toolchainService,
        IProfileLibraryService profileLibraryService,
        IEncodingJobRunner jobRunner,
        IQueueCompletionActionService queueCompletionActionService,
        ISystemIdleService systemIdleService,
        IAutoCompressionRunner autoCompressionRunner,
        IAudioProcessingRunner audioProcessingRunner,
        IAudioSourceInfoService audioSourceInfoService,
        IBluRayDiscProbeService bluRayDiscProbeService,
        IBluRayDemuxRunner bluRayDemuxRunner,
        LocalAppPaths appPaths,
        IAppSettingsService settingsService,
        ISetupGuideCacheService setupGuideCacheService,
        IToolRegistryService toolRegistryService,
        IToolProbeService toolProbeService,
        IEncoderDiscoveryService encoderDiscoveryService,
        IEnvironmentReadinessService environmentReadinessService,
        ISetupBootstrapService setupBootstrapService,
        IAppUpdateService appUpdateService)
    {
        _toolchainService = toolchainService;
        _profileLibraryService = profileLibraryService;
        _jobRunner = jobRunner;
        _queueCompletionActionService = queueCompletionActionService;
        _systemIdleService = systemIdleService;
        _autoCompressionRunner = autoCompressionRunner;
        _audioProcessingRunner = audioProcessingRunner;
        _audioSourceInfoService = audioSourceInfoService;
        _bluRayDiscProbeService = bluRayDiscProbeService;
        _bluRayDemuxRunner = bluRayDemuxRunner;
        _appPaths = appPaths;
        _settingsService = settingsService;
        _setupGuideCacheService = setupGuideCacheService;
        _toolRegistryService = toolRegistryService;
        _toolProbeService = toolProbeService;
        _encoderDiscoveryService = encoderDiscoveryService;
        _environmentReadinessService = environmentReadinessService;
        _setupBootstrapService = setupBootstrapService;
        _appUpdateService = appUpdateService;

        ReplaceItems(ThemeOptions, BuildThemeOptions());
        ReplaceItems(
            LanguageOptions,
            [
                new LanguageOption(AppLanguage.Chinese, "中文"),
                new LanguageOption(AppLanguage.English, "English")
            ]);
        ReplaceItems(
            EncoderOptions,
            [
                new EncoderOption(EncoderKind.X264, EncoderKind.X264.ToDisplayName()),
                new EncoderOption(EncoderKind.X265, EncoderKind.X265.ToDisplayName()),
                new EncoderOption(EncoderKind.SvtAv1, EncoderKind.SvtAv1.ToDisplayName())
            ]);
        ReplaceItems(ConcurrentEncodingJobOptions, BuildConcurrentEncodingJobOptions());
        ReplaceItems(QueueCompletionActionOptions, BuildQueueCompletionActionOptions());

        _selectedTheme = ThemeOptions[0];
        _selectedLanguage = LanguageOptions[0];
        _selectedEncoder = EncoderOptions[0];
        _selectedAutoEncoder = EncoderOptions[0];
        _selectedConcurrentEncodingJobOption = ConcurrentEncodingJobOptions[0];
        _selectedQueueCompletionActionOption = QueueCompletionActionOptions[0];
        _autoCompressionStatusText = _texts.AutoCompressionIdleStatus;
        InitializeAudioProcessingState();
        InitializeBluRayDemuxState();
    }

    public ObservableCollection<EncoderCatalogItem> Encoders { get; } = [];

    public ObservableCollection<SavedTemplate> UserTemplates { get; } = [];

    public ObservableCollection<TemplateLibraryItemViewModel> TemplateLibraryItems { get; } = [];

    public ObservableCollection<EncodingJobItemViewModel> Jobs { get; } = [];

    public ObservableCollection<DiscoveredEncoderBinary> DetectedSystemBinaries { get; } = [];

    public ObservableCollection<ThemeOption> ThemeOptions { get; } = [];

    public ObservableCollection<LanguageOption> LanguageOptions { get; } = [];

    public ObservableCollection<EncoderOption> EncoderOptions { get; } = [];

    public ObservableCollection<RateControlOption> AvailableRateControlModes { get; } = [];

    public ObservableCollection<StringChoiceOption> AvailablePresets { get; } = [];

    public ObservableCollection<StringChoiceOption> AvailableTunes { get; } = [];

    public ObservableCollection<StringChoiceOption> AvailableProfiles { get; } = [];

    public ObservableCollection<StringChoiceOption> AvailableOutputFormats { get; } = [];

    public ObservableCollection<StringChoiceOption> ConcurrentEncodingJobOptions { get; } = [];

    public ObservableCollection<StringChoiceOption> QueueCompletionActionOptions { get; } = [];

    public bool IsBusy => _isRefreshingCatalog
        || _isCheckingUpdates
        || _isDownloadingAppUpdateInstaller
        || _isSetupGuideInstallRunning
        || _isRefreshingSetupGuide
        || _isCheckingSetupDependencyUpdates;

    public bool IsCheckingAppUpdates => _isCheckingUpdates;

    public bool IsDownloadingAppUpdateInstaller => _isDownloadingAppUpdateInstaller;

    public bool IsAppUpdateActionInProgress => _isCheckingUpdates || _isDownloadingAppUpdateInstaller;

    public bool IsAppUpdateAvailable => _lastAppUpdateResult?.UpdateAvailable == true;

    public bool CanDownloadAppUpdateInstaller => _lastAppUpdateResult?.CanDownloadInstaller == true;

    public bool HasAppUpdateError => !string.IsNullOrWhiteSpace(_lastAppUpdateErrorMessage);

    public string AppUpdateActionText => IsCheckingAppUpdates
        ? Texts.CheckingUpdatesButton
        : IsDownloadingAppUpdateInstaller
            ? _appUpdateDownloadProgressPercent.HasValue
                ? Texts.DownloadingUpdateButtonWithProgress(_appUpdateDownloadProgressPercent.Value)
                : Texts.DownloadingUpdateButton
        : IsAppUpdateAvailable
            ? CanDownloadAppUpdateInstaller
                ? Texts.UpdateButton
                : Texts.ReleasePageButton
            : Texts.CheckUpdatesButton;

    public Symbol AppUpdateActionIcon => IsCheckingAppUpdates
        ? Symbol.Refresh
        : IsDownloadingAppUpdateInstaller
            ? Symbol.Download
            : IsAppUpdateAvailable
                ? CanDownloadAppUpdateInstaller
                    ? Symbol.Download
                    : Symbol.Link
                : Symbol.Refresh;

    public bool CanExecuteAppUpdateAction => !IsCheckingAppUpdates && !IsDownloadingAppUpdateInstaller;

    public Visibility AppUpdateProgressVisibility => IsAppUpdateActionInProgress
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string AppUpdateReleaseUrl => string.IsNullOrWhiteSpace(_lastAppUpdateResult?.ReleaseUrl)
        ? AppReleasePageUrl
        : _lastAppUpdateResult.ReleaseUrl;

    public string AppCurrentVersionText => Texts.AppCurrentVersionLabel(GetKnownCurrentAppVersion());

    public string AppLatestVersionText => string.IsNullOrWhiteSpace(_lastAppUpdateResult?.LatestVersion)
        ? string.Empty
        : Texts.AppLatestVersionLabel(_lastAppUpdateResult.LatestVersion);

    public Visibility AppLatestVersionVisibility => string.IsNullOrWhiteSpace(_lastAppUpdateResult?.LatestVersion)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public string AppUpdateStatusText => GetAppUpdateStatusText();

    public string EnvironmentCheckedAtText => GetSetupGuideLocalCheckedAtText();

    public string SetupGuideRemoteCheckedAtText => GetSetupGuideRemoteCheckedAtText();

    public Visibility SetupGuideRemoteCheckedAtVisibility => string.IsNullOrWhiteSpace(SetupGuideRemoteCheckedAtText)
        ? Visibility.Collapsed
        : Visibility.Visible;

    public bool IsRefreshingSetupGuide => _isRefreshingSetupGuide;

    public bool IsCheckingSetupDependencyUpdates => _isCheckingSetupDependencyUpdates;

    public Visibility SetupGuideActionProgressVisibility => _isRefreshingSetupGuide || _isCheckingSetupDependencyUpdates
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SetupGuideRefreshActionText => _isRefreshingSetupGuide
        ? Texts.SetupGuideRefreshingButton
        : Texts.SetupGuideRefreshButton;

    public string SetupGuideUpdateCheckActionText => _isCheckingSetupDependencyUpdates
        ? Texts.CheckingUpdatesButton
        : Texts.SetupGuideCheckUpdatesButton;

    public bool CanExecuteSetupGuideRefreshAction => !_isSetupGuideInstallRunning
        && !_isRefreshingSetupGuide
        && !_isCheckingSetupDependencyUpdates;

    public bool CanExecuteSetupGuideUpdateCheckAction => !_isSetupGuideInstallRunning
        && !_isRefreshingSetupGuide
        && !_isCheckingSetupDependencyUpdates;

    public AppText Texts
    {
        get => _texts;
        private set => SetProperty(ref _texts, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool HasRunningJobs => Jobs.Any(static job => job.State == EncodingJobState.Running);

    public string PreviewTitle
    {
        get => _previewTitle;
        private set => SetProperty(ref _previewTitle, value);
    }

    public string PreviewCommandLine
    {
        get => _previewCommandLine;
        private set => SetProperty(ref _previewCommandLine, value);
    }

    public string PreviewNotes
    {
        get => _previewNotes;
        private set => SetProperty(ref _previewNotes, value);
    }

    public string SelectedProfileCaption
    {
        get => _selectedProfileCaption;
        private set => SetProperty(ref _selectedProfileCaption, value);
    }

    public string DraftTemplateName
    {
        get => _draftTemplateName;
        set
        {
            if (SetProperty(ref _draftTemplateName, value))
            {
                OnPropertyChanged(nameof(HasUnsavedTemplateChanges));
            }
        }
    }

    public string DraftTemplateNotes
    {
        get => _draftTemplateNotes;
        set
        {
            if (SetProperty(ref _draftTemplateNotes, value))
            {
                OnPropertyChanged(nameof(HasUnsavedTemplateChanges));
            }
        }
    }

    public string TemplateSearchText
    {
        get => _templateSearchText;
        set
        {
            if (SetProperty(ref _templateSearchText, value))
            {
                RefreshTemplateLibraryItems();
            }
        }
    }

    public string SourcePath
    {
        get => _sourcePath;
        set
        {
            if (SetProperty(ref _sourcePath, value))
            {
                ScheduleDraftInputRefresh();
            }
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
            {
                if (!_isUpdatingOutputPath)
                {
                    _lastAutoOutputPath = null;
                }

                if (_isApplyingDeferredDraftInputRefresh)
                {
                    return;
                }

                ScheduleDraftInputRefresh();
            }
        }
    }

    public string AutoCompressionSourcePath
    {
        get => _autoCompressionSourcePath;
        set
        {
            if (SetProperty(ref _autoCompressionSourcePath, value))
            {
                ScheduleAutoCompressionInputRefresh();
            }
        }
    }

    public string AutoCompressionOutputPath
    {
        get => _autoCompressionOutputPath;
        set
        {
            if (SetProperty(ref _autoCompressionOutputPath, value))
            {
                if (!_isUpdatingAutoCompressionOutputPath)
                {
                    _lastAutoCompressionOutputPath = null;
                }

                if (_isApplyingDeferredAutoCompressionInputRefresh)
                {
                    return;
                }

                ScheduleAutoCompressionInputRefresh();
            }
        }
    }

    public EncoderOption? SelectedAutoEncoder
    {
        get => _selectedAutoEncoder;
        set
        {
            if (SetProperty(ref _selectedAutoEncoder, value))
            {
                ScheduleAutoCompressionInputRefresh();
            }
        }
    }

    public string AutoCompressionVideoParameters
    {
        get => _autoCompressionVideoParameters;
        set
        {
            if (SetProperty(ref _autoCompressionVideoParameters, value))
            {
                OnPropertyChanged(nameof(CanStartAutoCompression));
                RefreshAutoCompressionCommandPreview();
            }
        }
    }

    public double AutoCompressionTargetVmaf
    {
        get => _autoCompressionTargetVmaf;
        set
        {
            var normalized = Math.Clamp(value, 1, 100);
            if (SetProperty(ref _autoCompressionTargetVmaf, normalized))
            {
                OnPropertyChanged(nameof(CanStartAutoCompression));
                OnPropertyChanged(nameof(AutoCompressionSuggestedOutputFileName));
                OnPropertyChanged(nameof(AutoCompressionOutputPreviewText));
                RefreshAutoCompressionCommandPreview();
            }
        }
    }

    public double AutoCompressionProbes
    {
        get => _autoCompressionProbes;
        set
        {
            var normalized = Math.Max(1, value);
            if (SetProperty(ref _autoCompressionProbes, normalized))
            {
                OnPropertyChanged(nameof(CanStartAutoCompression));
                RefreshAutoCompressionCommandPreview();
            }
        }
    }

    public double AutoCompressionWorkers
    {
        get => _autoCompressionWorkers;
        set
        {
            var normalized = Math.Max(0, value);
            if (SetProperty(ref _autoCompressionWorkers, normalized))
            {
                OnPropertyChanged(nameof(CanStartAutoCompression));
                RefreshAutoCompressionCommandPreview();
            }
        }
    }

    public ThemeOption? SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            SetProperty(ref _selectedTheme, value);
        }
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                ApplyLanguage(CurrentLanguagePreference);
            }
        }
    }

    public EncoderOption? SelectedEncoder
    {
        get => _selectedEncoder;
        set
        {
            if (SetProperty(ref _selectedEncoder, value) && !_isSynchronizingDraft)
            {
                ApplyCapabilityDefaults();
                FinalizeDraftChange(syncOutputPath: true, markAsCustomized: true);
            }
        }
    }

    public RateControlOption? SelectedRateControl
    {
        get => _selectedRateControl;
        set
        {
            if (SetProperty(ref _selectedRateControl, value) && !_isSynchronizingDraft)
            {
                OnPropertyChanged(nameof(IsQualityControlVisible));
                OnPropertyChanged(nameof(IsBitrateControlVisible));
                OnPropertyChanged(nameof(DraftQualityVisibility));
                OnPropertyChanged(nameof(DraftBitrateVisibility));
                OnPropertyChanged(nameof(QualityInputLabel));
                OnPropertyChanged(nameof(BitrateInputLabel));
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public StringChoiceOption? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public StringChoiceOption? SelectedTune
    {
        get => _selectedTune;
        set
        {
            if (SetProperty(ref _selectedTune, value) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public StringChoiceOption? SelectedProfileOption
    {
        get => _selectedProfileOption;
        set
        {
            if (SetProperty(ref _selectedProfileOption, value) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public StringChoiceOption? SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set
        {
            if (SetProperty(ref _selectedOutputFormat, value) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: true, markAsCustomized: true);
            }
        }
    }

    public StringChoiceOption? SelectedConcurrentEncodingJobOption
    {
        get => _selectedConcurrentEncodingJobOption;
        set
        {
            if (!SetProperty(ref _selectedConcurrentEncodingJobOption, value) || value is null)
            {
                return;
            }

            if (int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var concurrentJobCount))
            {
                MaxConcurrentEncodingJobs = concurrentJobCount;
            }
        }
    }

    public StringChoiceOption? SelectedQueueCompletionActionOption
    {
        get => _selectedQueueCompletionActionOption;
        set
        {
            if (!SetProperty(ref _selectedQueueCompletionActionOption, value) || value is null)
            {
                return;
            }

            if (Enum.TryParse<QueueCompletionAction>(value.Value, ignoreCase: false, out var action))
            {
                QueueCompletionAction = action;
            }
        }
    }

    public double DraftQuality
    {
        get => _draftQuality;
        set
        {
            var normalized = Math.Max(0, value);
            if (SetProperty(ref _draftQuality, normalized) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public double DraftBitrate
    {
        get => _draftBitrate;
        set
        {
            var normalized = Math.Max(1, value);
            if (SetProperty(ref _draftBitrate, normalized) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public string DraftAdditionalArguments
    {
        get => _draftAdditionalArguments;
        set
        {
            if (SetProperty(ref _draftAdditionalArguments, value) && !_isSynchronizingDraft)
            {
                ApplyManualArgumentOverrides(value);
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public string DraftUhdParameters
    {
        get => _draftUhdParameters;
        set
        {
            if (SetProperty(ref _draftUhdParameters, value) && !_isSynchronizingDraft)
            {
                FinalizeDraftChange(syncOutputPath: false, markAsCustomized: true);
            }
        }
    }

    public bool PreferSystemEncoders
    {
        get => _preferSystemEncoders;
        set
        {
            if (SetProperty(ref _preferSystemEncoders, value))
            {
                SchedulePreviewRefresh();
            }
        }
    }

    public bool AutoCheckUpdatesOnStartup
    {
        get => _autoCheckUpdatesOnStartup;
        set
        {
            SetProperty(ref _autoCheckUpdatesOnStartup, value);
        }
    }

    public double MaxConcurrentEncodingJobs
    {
        get => _maxConcurrentEncodingJobs;
        set
        {
            var normalized = NormalizeConcurrentEncodingJobs(value);
            if (SetProperty(ref _maxConcurrentEncodingJobs, normalized))
            {
                SyncSelectedConcurrentEncodingJobOption(normalized);
                _ = ProcessQueueAsync();
            }
        }
    }

    public QueueCompletionAction QueueCompletionAction
    {
        get => _queueCompletionAction;
        set
        {
            if (!SetProperty(ref _queueCompletionAction, value))
            {
                return;
            }

            SyncSelectedQueueCompletionActionOption(value);

            if (value == QueueCompletionAction.None)
            {
                CancelPendingQueueCompletionActionWait();
                ResetQueueCompletionActionBatch();
            }
            else if (Jobs.Any(static job => job.State == EncodingJobState.Running))
            {
                _isQueueCompletionActionArmed = true;
            }
            else if (_queueCompletionActionWaitCancellationTokenSource is not null && !HasActiveAppWork())
            {
                StatusText = Texts.QueueCompletionActionPendingIdleStatus(value, QueueCompletionActionIdleRequirement);
            }
        }
    }

    public string WorkspaceRootPath
    {
        get => _workspaceRootPath;
        set => SetProperty(ref _workspaceRootPath, value);
    }

    public string TemplateFilesRootPath => _appPaths.WorkspaceTemplatesRootPath;

    public EncodingJobItemViewModel? SelectedJob
    {
        get => _selectedJob;
        private set
        {
            if (ReferenceEquals(_selectedJob, value))
            {
                return;
            }

            if (_selectedJob is not null)
            {
                _selectedJob.PropertyChanged -= SelectedJob_PropertyChanged;
            }

            if (SetProperty(ref _selectedJob, value))
            {
                if (_selectedJob is not null)
                {
                    _selectedJob.PropertyChanged += SelectedJob_PropertyChanged;
                }

                RaiseSelectedJobPropertyChanges();
            }
        }
    }

    public string AutoCompressionStatusText
    {
        get => _autoCompressionStatusText;
        private set => SetProperty(ref _autoCompressionStatusText, value);
    }

    public string AutoCompressionCommandLine
    {
        get => _autoCompressionCommandLine;
        private set => SetProperty(ref _autoCompressionCommandLine, value);
    }

    public string AutoCompressionLog
    {
        get => _autoCompressionLog;
        private set => SetProperty(ref _autoCompressionLog, value);
    }

    public double AutoCompressionProgressPercent
    {
        get => _autoCompressionProgressPercent;
        private set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (SetProperty(ref _autoCompressionProgressPercent, normalized))
            {
                OnPropertyChanged(nameof(AutoCompressionProgressLabel));
            }
        }
    }

    public bool AutoCompressionProgressIsIndeterminate
    {
        get => _autoCompressionProgressIsIndeterminate;
        private set
        {
            if (SetProperty(ref _autoCompressionProgressIsIndeterminate, value))
            {
                OnPropertyChanged(nameof(AutoCompressionProgressLabel));
                OnPropertyChanged(nameof(AutoCompressionProgressHintVisibility));
            }
        }
    }

    public string ToolsetRootPath => _toolchainService.GetToolsetRootPath();

    public string SuggestedOutputExtension => _activeProfile?.OutputContainer ?? "264";

    public string QualityInputLabel => SelectedRateControl?.Value switch
    {
        RateControlMode.Cq => "CQ",
        RateControlMode.Qp => "QP",
        _ => "CRF"
    };

    public string BitrateInputLabel => SelectedRateControl?.Value == RateControlMode.TwoPass
        ? Texts.Pick("目标码率 (2-Pass)", "Target Bitrate (2-Pass)")
        : Texts.Pick("目标码率", "Target Bitrate");

    public bool IsQualityControlVisible => SelectedRateControl?.Value is RateControlMode.Crf or RateControlMode.Cq or RateControlMode.Qp;

    public bool IsBitrateControlVisible => SelectedRateControl?.Value is RateControlMode.Abr or RateControlMode.Vbr or RateControlMode.TwoPass;

    public bool IsX265Selected => SelectedEncoder?.Value == EncoderKind.X265;

    public Visibility DraftQualityVisibility => IsQualityControlVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DraftBitrateVisibility => IsBitrateControlVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility X265UhdVisibility => IsX265Selected ? Visibility.Visible : Visibility.Collapsed;

    public string DraftConstraintWarningText => GetProfileConstraintError(_activeProfile) ?? string.Empty;

    public Visibility DraftConstraintWarningVisibility =>
        string.IsNullOrWhiteSpace(DraftConstraintWarningText) ? Visibility.Collapsed : Visibility.Visible;

    public string SuggestedOutputFileName
    {
        get
        {
            var outputPath = TryResolveDraftOutputPreviewPath();
            return string.IsNullOrWhiteSpace(outputPath)
                ? Texts.SuggestedOutputName
                : Path.GetFileNameWithoutExtension(outputPath);
        }
    }

    public string DraftOutputPreviewText => _isDraftInputRefreshPending
        ? Texts.OutputPreviewUpdating
        : BuildOutputPreviewText(TryResolveDraftOutputPreviewPath());

    public bool CanQueueJob =>
        _activeProfile is not null
        && !string.IsNullOrWhiteSpace(SourcePath)
        && !string.IsNullOrWhiteSpace(OutputPath);

    public bool IsAutoCompressionRunning => _isAutoCompressionRunning;

    public bool CanStartAutoCompression =>
        !_isAutoCompressionRunning
        && SelectedAutoEncoder is not null
        && !string.IsNullOrWhiteSpace(AutoCompressionSourcePath)
        && !string.IsNullOrWhiteSpace(AutoCompressionOutputPath);

    public bool CanCancelAutoCompression => _isAutoCompressionRunning;

    public string AutoCompressionProgressLabel =>
        AutoCompressionProgressIsIndeterminate && _isAutoCompressionRunning
            ? Texts.AutoCompressionProgressActiveLabel
            : $"{AutoCompressionProgressPercent:0.#}%";

    public Visibility AutoCompressionProgressHintVisibility =>
        _isAutoCompressionRunning && AutoCompressionProgressIsIndeterminate
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string AutoCompressionProgressHint => Texts.AutoCompressionProgressIndeterminateHint;

    public Brush AutoCompressionStatusPanelBorderBrush => ResolveTaskStatusPanelBorderBrush(_autoCompressionDisplayState);

    public string AutoCompressionSuggestedOutputExtension => "mkv";

    public string AutoCompressionSuggestedOutputFileName
    {
        get
        {
            var outputPath = TryResolveAutoCompressionOutputPreviewPath();
            return string.IsNullOrWhiteSpace(outputPath)
                ? Texts.SuggestedOutputName
                : Path.GetFileNameWithoutExtension(outputPath);
        }
    }

    public string AutoCompressionOutputPreviewText => _isAutoCompressionInputRefreshPending
        ? Texts.OutputPreviewUpdating
        : BuildOutputPreviewText(TryResolveAutoCompressionOutputPreviewPath());

    public string QueueSummary
    {
        get
        {
            if (Jobs.Count == 0)
            {
                return Texts.NoQueueJobs;
            }

            var running = Jobs.Count(static job => job.State == EncodingJobState.Running);
            var queued = Jobs.Count(static job => job.State == EncodingJobState.Queued);
            var completed = Jobs.Count(static job => job.State == EncodingJobState.Completed);
            var failed = Jobs.Count(static job => job.State == EncodingJobState.Failed);
            var cancelled = Jobs.Count(static job => job.State == EncodingJobState.Cancelled);

            return Texts.QueueSummary(running, queued, completed, failed, cancelled);
        }
    }

    public bool HasJobs => Jobs.Count > 0;

    public Visibility EmptyQueueVisibility => HasJobs ? Visibility.Collapsed : Visibility.Visible;

    public int SelectedQueueJobCount => _selectedQueueJobs.Count;

    public int SelectedQueuedJobCount => _selectedQueueJobs.Count(static job => job.State == EncodingJobState.Queued);

    public int SelectedRunningJobCount => _selectedQueueJobs.Count(static job => job.State == EncodingJobState.Running);

    public int SelectedCancelableQueueJobCount => _selectedQueueJobs.Count(static job => job.CanCancel);

    public int SelectedRemovableQueueJobCount => _selectedQueueJobs.Count(static job => job.CanRemove);

    public string QueueSelectionStatusText => Texts.QueueSelectionStatus(SelectedQueueJobCount, Jobs.Count);

    public bool CanSelectAllQueueJobs => Jobs.Count > 0 && SelectedQueueJobCount < Jobs.Count;

    public bool CanInvertQueueSelection => Jobs.Count > 0;

    public bool CanClearQueueSelection => SelectedQueueJobCount > 0;

    public bool CanStartSelectedJobs => _selectedQueueJobs.Any(static job => job.CanStart);

    public bool CanCancelSelectedJobs => _selectedQueueJobs.Any(static job => job.CanCancel);

    public bool CanDeleteSelectedJobs => _selectedQueueJobs.Any(static job => job.CanRemove);

    private bool IsQueueBatchSelectionActive => SelectedQueueJobCount > 1;

    public double SelectedJobProgressValue => IsQueueBatchSelectionActive ? 0.0 : SelectedJob?.ProgressValue ?? 0.0;

    public string SelectedJobProgressPrimaryText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionSummary(
            SelectedQueueJobCount,
            SelectedRunningJobCount,
            SelectedQueuedJobCount,
            GetSelectedQueueJobStateCount(EncodingJobState.Completed),
            GetSelectedQueueJobStateCount(EncodingJobState.Failed),
            GetSelectedQueueJobStateCount(EncodingJobState.Cancelled))
        : SelectedJob?.ProgressTelemetryPrimaryLine ?? Texts.DefaultProgressPrimary;

    public string SelectedJobProgressSecondaryText => IsQueueBatchSelectionActive
        ? Texts.QueueSelectionStatus(SelectedQueueJobCount, Jobs.Count)
        : SelectedJob?.ProgressTelemetrySecondaryLine ?? Texts.DefaultProgressSecondary;

    public string SelectedJobProgressPercentText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionProgressLabel(SelectedQueueJobCount)
        : SelectedJob?.ProgressPercentLabel ?? "0%";

    public Visibility SelectedJobSourcePreparationVisibility => !IsQueueBatchSelectionActive && SelectedJob?.HasSourcePreparationText == true
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SelectedJobSourcePreparationText => IsQueueBatchSelectionActive
        ? string.Empty
        : SelectedJob?.SourcePreparationText ?? string.Empty;

    public string SelectedJobFramesText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionQueuedMetric(SelectedQueuedJobCount)
        : BuildSelectedJobFramesText();

    public string SelectedJobFpsText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionRunningMetric(SelectedRunningJobCount)
        : SelectedJob?.FramesPerSecond is > 0
            ? $"{SelectedJob.FramesPerSecond.Value:0.00} fps"
            : "--.-- fps";

    public string SelectedJobBitrateText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionCancelableMetric(SelectedCancelableQueueJobCount)
        : SelectedJob?.BitrateKbps is > 0
            ? $"{SelectedJob.BitrateKbps.Value:0.00} kb/s"
            : "--.-- kb/s";

    public string SelectedJobEtaText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionRemovableMetric(SelectedRemovableQueueJobCount)
        : $"{Texts.EtaPrefix} {FormatSelectedJobEta(SelectedJob?.Eta)}";

    public string SelectedJobEstimatedSizeText => IsQueueBatchSelectionActive
        ? Texts.QueueSelectionStatus(SelectedQueueJobCount, Jobs.Count)
        : $"{Texts.EstimatedSizePrefix} {FormatSelectedJobSize(SelectedJob?.EstimatedFileSizeBytes)}";

    public string SelectedJobCommandText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionCommandText
        : SelectedJob?.DisplayCommand ?? Texts.SelectJobForCommandText;

    public string SelectedJobLogText => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionLogText
        : SelectedJob is null
            ? Texts.SelectJobForLogText
            : string.IsNullOrWhiteSpace(SelectedJob.Log)
                ? Texts.NoSelectedJobLogText
                : SelectedJob.Log;

    public Visibility TemplateLibraryEmptyVisibility => TemplateLibraryItems.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string? EditingUserTemplateId => _editingTemplateId;

    public string? CurrentTemplateSelectionKey => _currentTemplateSelectionKey;

    public bool IsEditingPinnedTemplate => GetEditingUserTemplate()?.IsPinned == true;

    public bool CanEditTemplateDraft => !IsEditingPinnedTemplate;

    public bool HasUnsavedTemplateChanges => !MatchesTemplateEditingBaseline();

    public string SelectedJobSummary => IsQueueBatchSelectionActive
        ? Texts.QueueBatchSelectionSummary(
            SelectedQueueJobCount,
            SelectedRunningJobCount,
            SelectedQueuedJobCount,
            GetSelectedQueueJobStateCount(EncodingJobState.Completed),
            GetSelectedQueueJobStateCount(EncodingJobState.Failed),
            GetSelectedQueueJobStateCount(EncodingJobState.Cancelled))
        : SelectedJob is null
            ? Texts.SelectedJobSummaryPlaceholder
            : Texts.QueueSelectionSummary(SelectedJob.StateLabel, SelectedJob.Summary);

    public AppThemePreference CurrentThemePreference => SelectedTheme?.Value ?? AppThemePreference.Default;

    public AppLanguage CurrentLanguagePreference => SelectedLanguage?.Value ?? AppLanguage.Chinese;

    public async Task InitializeAsync()
    {
        LoadSettings();
        var restoredSetupGuideSnapshot = TryRestoreSetupGuideSnapshot();
        await RefreshAsync(
            Texts.InitializationStatus,
            includeUpdates: AutoCheckUpdatesOnStartup,
            refreshEnvironmentReadiness: !restoredSetupGuideSnapshot);

        RunInitialVsPluginDependencyUpdateIfNeeded();

        if (!_hasCompletedSetupGuide)
        {
            await RefreshSetupGuideStatusAsync(
                includeRemoteMetadata: false,
                statusOverride: null,
                openWhenFinished: false,
                forceEnvironmentScan: false,
                preferCachedSnapshot: false);
            OpenSetupGuide();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        CancelPendingQueueCompletionActionWait();
        CancelPendingPreviewRefresh();
        CancelPendingDraftInputRefresh();
        CancelPendingAutoCompressionInputRefresh();
        CancelAutoCompression();
        DisposeAutoCompressionCancellation();
        DisposeAudioProcessingState();
        DisposeBluRayDemuxState();

        if (_selectedJob is not null)
        {
            _selectedJob.PropertyChanged -= SelectedJob_PropertyChanged;
        }
    }

    public async Task RefreshAsync(
        string? statusOverride = null,
        bool includeUpdates = false,
        bool refreshEnvironmentReadiness = true)
    {
        if (_isRefreshingCatalog)
        {
            return;
        }

        _isRefreshingCatalog = true;
        OnPropertyChanged(nameof(IsBusy));

        try
        {
            var encoderCatalogTask = _toolchainService.GetCatalogAsync();
            var userTemplatesTask = _profileLibraryService.GetUserTemplatesAsync();
            var environmentReadinessTask = refreshEnvironmentReadiness || _environmentReadinessReport is null
                ? _environmentReadinessService.CheckAsync()
                : null;

            if (environmentReadinessTask is not null)
            {
                await Task.WhenAll(
                    encoderCatalogTask,
                    userTemplatesTask,
                    environmentReadinessTask);
            }
            else
            {
                await Task.WhenAll(
                    encoderCatalogTask,
                    userTemplatesTask);
            }

            var encoderCatalog = await encoderCatalogTask;
            var userTemplates = await userTemplatesTask;

            ReplaceItems(Encoders, encoderCatalog);
            ReplaceItems(UserTemplates, userTemplates);
            RefreshTemplateLibraryItems();
            RefreshEncoderOptions();

            if (environmentReadinessTask is not null)
            {
                ApplyEnvironmentReadiness(await environmentReadinessTask);
            }

            await RefreshSystemBinariesAsync();

            RaiseSummaryPropertyChanges();

            if (_activeProfile is null)
            {
                BeginNewTemplateDraft();
            }
            else
            {
                ApplyProfileToDraft(_activeProfile, SelectedProfileCaption, DraftTemplateName, DraftTemplateNotes);
                await RefreshPreviewNowAsync(_activeProfile);
            }

            if (includeUpdates)
            {
                await RefreshAvailableUpdatesAsync(reportStatus: false);
            }

            StatusText = statusOverride ?? Texts.RefreshCompletedStatus(DateTime.Now);
        }
        catch (Exception ex)
        {
            StatusText = Texts.RefreshFailedStatus(ex.Message);
        }
        finally
        {
            _isRefreshingCatalog = false;
            OnPropertyChanged(nameof(IsBusy));
        }
    }

    public async Task<AppUpdateCheckResult?> RefreshAvailableUpdatesAsync(bool reportStatus = true)
    {
        if (_isCheckingUpdates)
        {
            return null;
        }

        _isCheckingUpdates = true;
        _lastAppUpdateErrorMessage = null;
        RaiseAppUpdatePropertyChanges();

        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync();
            _lastAppUpdateResult = result;
            _lastAppUpdateErrorMessage = null;
            RaiseAppUpdatePropertyChanges();

            if (reportStatus)
            {
                StatusText = AppUpdateStatusText;
            }

            return result;
        }
        catch (Exception ex)
        {
            _lastAppUpdateErrorMessage = Texts.UpdatesCheckFailedStatus(ex.Message);
            RaiseAppUpdatePropertyChanges();
            if (reportStatus)
            {
                StatusText = AppUpdateStatusText;
            }

            return null;
        }
        finally
        {
            _isCheckingUpdates = false;
            RaiseAppUpdatePropertyChanges();
        }
    }

    public async Task<string?> DownloadLatestAppInstallerAsync(bool reportStatus = true)
    {
        if (_isCheckingUpdates || _isDownloadingAppUpdateInstaller)
        {
            return null;
        }

        if (_lastAppUpdateResult is not { UpdateAvailable: true })
        {
            return null;
        }

        _isDownloadingAppUpdateInstaller = true;
        _appUpdateDownloadProgressPercent = null;
        _lastAppUpdateErrorMessage = null;
        RaiseAppUpdatePropertyChanges();

        if (reportStatus)
        {
            StatusText = Texts.AppUpdateDownloadingStatus(_lastAppUpdateResult.LatestVersion, _appUpdateDownloadProgressPercent);
        }

        try
        {
            var progress = new Progress<double>(value =>
            {
                var percent = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * 100.0);
                if (_appUpdateDownloadProgressPercent == percent)
                {
                    return;
                }

                _appUpdateDownloadProgressPercent = percent;
                RaiseAppUpdatePropertyChanges();

                if (reportStatus)
                {
                    StatusText = AppUpdateStatusText;
                }
            });

            var installerPath = await _appUpdateService.DownloadInstallerAsync(_lastAppUpdateResult, progress);
            _lastAppUpdateErrorMessage = null;
            RaiseAppUpdatePropertyChanges();

            if (reportStatus)
            {
                StatusText = Texts.AppUpdateInstallerReadyStatus;
            }

            return installerPath;
        }
        catch (Exception ex)
        {
            _lastAppUpdateErrorMessage = Texts.AppUpdateDownloadFailedStatus(ex.Message);
            RaiseAppUpdatePropertyChanges();
            if (reportStatus)
            {
                StatusText = AppUpdateStatusText;
            }

            return null;
        }
        finally
        {
            _isDownloadingAppUpdateInstaller = false;
            _appUpdateDownloadProgressPercent = null;
            RaiseAppUpdatePropertyChanges();
        }
    }

    public string? SaveSettings(bool updateStatusText = true)
    {
        try
        {
            var normalizedWorkspaceRootPath = _appPaths.NormalizeWorkspaceRootPath(WorkspaceRootPath);
            var settings = new AppSettings(
                PreferSystemEncoders,
                AutoCheckUpdatesOnStartup,
                CurrentThemePreference,
                CurrentLanguagePreference,
                _hasCompletedSetupGuide,
                normalizedWorkspaceRootPath,
                new Dictionary<string, string>(_manualToolPaths, StringComparer.OrdinalIgnoreCase),
                _hasRunInitialVsPluginDependencyUpdate,
                GetMaxConcurrentEncodingJobCount(),
                QueueCompletionAction);

            _settingsService.Save(settings);
            _encoderDiscoveryService.InvalidateCache();
            _toolProbeService.InvalidateCache();
            WorkspaceRootPath = normalizedWorkspaceRootPath;
            if (updateStatusText)
            {
                StatusText = string.Equals(normalizedWorkspaceRootPath, _appPaths.RootPath, StringComparison.OrdinalIgnoreCase)
                    ? Texts.SettingsSavedStatus
                    : Texts.WorkspaceDirectorySavedStatus;
            }

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public void SelectJob(EncodingJobItemViewModel? job)
    {
        SelectedJob = job;
    }

    public void UpdateSelectedQueueJobs(IEnumerable<EncodingJobItemViewModel> selectedJobs)
    {
        var normalizedSelection = NormalizeSelectedQueueJobs(selectedJobs).ToList();
        if (_selectedQueueJobs.Count == normalizedSelection.Count
            && _selectedQueueJobs.SequenceEqual(normalizedSelection))
        {
            return;
        }

        _selectedQueueJobs.Clear();
        _selectedQueueJobs.AddRange(normalizedSelection);
        RaiseQueueSelectionPropertyChanges();
    }

    public async Task SelectUserTemplateAsync(SavedTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        ApplyProfileToDraft(
            template.Profile,
            Texts.UserCaption(template.Name),
            template.Name,
            template.Notes);

        if (_activeProfile is not null)
        {
            await RefreshPreviewNowAsync(_activeProfile);
        }

        CaptureTemplateEditingBaseline(
            template.Id,
            $"user:{template.Id}",
            template.Name,
            template.Notes,
            _activeProfile);
    }

    public async Task ApplyUserTemplateToEncodingDraftAsync(SavedTemplate? template)
    {
        if (template is null)
        {
            return;
        }

        ApplyProfileToDraft(
            template.Profile,
            Texts.UserCaption(template.Name),
            template.Name,
            template.Notes);

        if (_activeProfile is not null)
        {
            await RefreshPreviewNowAsync(_activeProfile);
        }

        CaptureTemplateEditingBaseline(
            null,
            null,
            template.Name,
            template.Notes,
            _activeProfile);
    }

    public Task<SavedTemplate> ReadTemplateAsync(string filePath)
    {
        return _profileLibraryService.ReadTemplateAsync(filePath);
    }

    public async Task<SavedTemplate?> SaveCurrentTemplateAsync()
    {
        if (_activeProfile is null || string.IsNullOrWhiteSpace(DraftTemplateName))
        {
            return null;
        }

        var normalizedTemplateName = DraftTemplateName?.Trim() ?? string.Empty;
        var normalizedTemplateNotes = DraftTemplateNotes?.Trim() ?? string.Empty;
        var editingTemplate = GetEditingUserTemplate();
        if (editingTemplate?.IsPinned == true
            && string.Equals(editingTemplate.Name, normalizedTemplateName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Texts.PinnedTemplateLockedMessage);
        }

        var profileToSave = _activeProfile with
        {
            Name = normalizedTemplateName,
            Description = normalizedTemplateNotes
        };

        return await PersistUserTemplateAsync(
            normalizedTemplateName,
            normalizedTemplateNotes,
            profileToSave,
            editingTemplate?.IsPinned == true ? null : _editingTemplateId,
            isPinned: false,
            Texts.TemplateSavedStatus(normalizedTemplateName));
    }

    public async Task<SavedTemplate> ImportTemplateAsync(SavedTemplate template, string? overwriteTemplateId = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        var normalizedTemplateName = template.Name?.Trim() ?? string.Empty;
        var normalizedTemplateNotes = template.Notes?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTemplateName))
        {
            throw new InvalidOperationException(Texts.EmptyTemplateNameMessage);
        }

        var profileToSave = template.Profile with
        {
            Name = normalizedTemplateName,
            Description = normalizedTemplateNotes
        };

        return await PersistUserTemplateAsync(
            normalizedTemplateName,
            normalizedTemplateNotes,
            profileToSave,
            overwriteTemplateId,
            template.IsPinned,
            Texts.TemplateImportedStatus(normalizedTemplateName));
    }

    public async Task ExportCurrentTemplateAsync(string filePath)
    {
        var exportTemplate = BuildDraftTemplateForExchange();
        if (exportTemplate is null)
        {
            throw new InvalidOperationException(Texts.TemplateExportUnavailableMessage);
        }

        await _profileLibraryService.ExportTemplateAsync(exportTemplate, filePath);
        StatusText = Texts.TemplateExportedStatus(filePath);
    }

    public SavedTemplate? FindUserTemplateByName(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        var normalizedTemplateName = templateName.Trim();
        return UserTemplates.FirstOrDefault(template =>
            string.Equals(template.Name, normalizedTemplateName, StringComparison.OrdinalIgnoreCase));
    }

    public SavedTemplate? FindUserTemplateById(string? templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return null;
        }

        return UserTemplates.FirstOrDefault(template =>
            string.Equals(template.Id, templateId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        var currentTemplate = FindUserTemplateById(templateId);
        if (currentTemplate?.IsPinned == true)
        {
            throw new InvalidOperationException(Texts.PinnedTemplateLockedMessage);
        }

        await _profileLibraryService.DeleteTemplateAsync(templateId);
        ReplaceItems(UserTemplates, await _profileLibraryService.GetUserTemplatesAsync());
        RefreshTemplateLibraryItems();

        if (string.Equals(_editingTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
        {
            BeginNewTemplateDraft();
        }

        RaiseSummaryPropertyChanges();
        StatusText = Texts.UserTemplateDeletedStatus;
    }

    public async Task<SavedTemplate> SetTemplatePinnedAsync(string templateId, bool isPinned)
    {
        var template = FindUserTemplateById(templateId);
        if (template is null)
        {
            ReplaceItems(UserTemplates, await _profileLibraryService.GetUserTemplatesAsync());
            RefreshTemplateLibraryItems();
            template = FindUserTemplateById(templateId);
        }

        if (template is null)
        {
            throw new InvalidOperationException(Texts.TemplateMissingMessage);
        }

        var updatedTemplate = await _profileLibraryService.SetTemplatePinnedAsync(template.Id, isPinned);
        ReplaceItems(UserTemplates, await _profileLibraryService.GetUserTemplatesAsync());
        RefreshTemplateLibraryItems();
        RaiseTemplateLockPropertyChanges();
        RaiseSummaryPropertyChanges();
        StatusText = isPinned
            ? Texts.TemplatePinnedStatus(updatedTemplate.Name)
            : Texts.TemplateUnpinnedStatus(updatedTemplate.Name);
        return updatedTemplate;
    }

    public void RefreshTemplateLibraryView()
    {
        RefreshTemplateLibraryItems();
    }

    public void BeginNewTemplateDraft()
    {
        var targetKind = SelectedEncoder?.Value ?? _activeProfile?.Kind ?? EncoderKind.X264;

        _isSynchronizingDraft = true;

        try
        {
            SelectedEncoder = EncoderOptions.FirstOrDefault(option => option.Value == targetKind)
                ?? EncoderOptions.FirstOrDefault();
            ApplyCapabilityDefaults();
            DraftTemplateName = string.Empty;
            DraftTemplateNotes = string.Empty;
        }
        finally
        {
            _isSynchronizingDraft = false;
        }

        FinalizeDraftChange(syncOutputPath: false, markAsCustomized: false);
        SelectedProfileCaption = Texts.NewTemplateCaption;
        CaptureTemplateEditingBaseline(
            null,
            null,
            DraftTemplateName,
            DraftTemplateNotes,
            _activeProfile);
    }

    public string? ValidateAutoCompressionForStart(out string? existingOutputPath)
    {
        existingOutputPath = null;

        try
        {
            var request = CreateAutoCompressionRequest(requireSourceExists: true);
            existingOutputPath = File.Exists(request.OutputPath) ? request.OutputPath : null;
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> StartAutoCompressionAsync()
    {
        if (_isAutoCompressionRunning)
        {
            return Texts.AutoCompressionAlreadyRunningError;
        }

        AutoCompressionResult result;
        string sourceFileName;

        try
        {
            var request = CreateAutoCompressionRequest(requireSourceExists: true);
            sourceFileName = Path.GetFileName(request.SourcePath);

            ResetAutoCompressionLogState();
            AutoCompressionLog = string.Empty;
            AutoCompressionProgressPercent = 0;
            AutoCompressionProgressIsIndeterminate = true;
            SetAutoCompressionDisplayState(EncodingJobState.Running);
            AutoCompressionCommandLine = _autoCompressionRunner.BuildDisplayCommand(request);
            AutoCompressionStatusText = Texts.AutoCompressionStartingStatus(sourceFileName);
            StatusText = Texts.AutoCompressionStartingStatus(sourceFileName);

            SetAutoCompressionRunningState(true, request.JobId);

            _autoCompressionCancellationTokenSource = new CancellationTokenSource();

            var progress = new Progress<AutoCompressionProgress>(ApplyAutoCompressionProgress);
            result = await _autoCompressionRunner.RunAsync(
                request,
                progress,
                _autoCompressionCancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_autoCompressionCancellationTokenSource?.IsCancellationRequested == true)
        {
            SetAutoCompressionRunningState(false, null);
            DisposeAutoCompressionCancellation();
            SetAutoCompressionDisplayState(EncodingJobState.Cancelled);
            ClampAutoCompressionProgressForTerminalState(EncodingJobState.Cancelled);
            AutoCompressionStatusText = Texts.AutoCompressionCancelledStatus;
            StatusText = Texts.AutoCompressionCancelledStatus;
            return null;
        }
        catch (Exception ex)
        {
            SetAutoCompressionRunningState(false, null);
            DisposeAutoCompressionCancellation();
            ClampAutoCompressionProgressForTerminalState(EncodingJobState.Failed);
            SetAutoCompressionDisplayState(EncodingJobState.Failed);
            AppendAutoCompressionLogLine(ex.Message);
            AutoCompressionStatusText = Texts.AutoCompressionFailedStatus(ex.Message);
            StatusText = Texts.AutoCompressionFailedStatus(ex.Message);
            return ex.Message;
        }

        DisposeAutoCompressionCancellation();
        SetAutoCompressionRunningState(false, null);

        if (string.IsNullOrWhiteSpace(AutoCompressionLog))
        {
            AutoCompressionLog = result.Log;
        }

        switch (result.State)
        {
            case EncodingJobState.Completed:
                SetAutoCompressionDisplayState(EncodingJobState.Completed);
                ClampAutoCompressionProgressForTerminalState(EncodingJobState.Completed);
                AutoCompressionStatusText = Texts.AutoCompressionCompletedStatus;
                StatusText = Texts.AutoCompressionCompletedStatus;
                return null;

            case EncodingJobState.Cancelled:
                SetAutoCompressionDisplayState(EncodingJobState.Cancelled);
                ClampAutoCompressionProgressForTerminalState(EncodingJobState.Cancelled);
                AutoCompressionStatusText = Texts.AutoCompressionCancelledStatus;
                StatusText = Texts.AutoCompressionCancelledStatus;
                return null;

            default:
                SetAutoCompressionDisplayState(EncodingJobState.Failed);
                ClampAutoCompressionProgressForTerminalState(EncodingJobState.Failed);
                AppendAutoCompressionLogLine(result.Summary);
                AutoCompressionStatusText = Texts.AutoCompressionFailedStatus(result.Summary);
                StatusText = Texts.AutoCompressionFailedStatus(result.Summary);
                return result.Summary;
        }
    }

    public void CancelAutoCompression()
    {
        if (!_isAutoCompressionRunning)
        {
            return;
        }

        AutoCompressionStatusText = Texts.AutoCompressionCancellingStatus;
        StatusText = Texts.AutoCompressionCancellingStatus;

        _autoCompressionCancellationTokenSource?.Cancel();
        if (_activeAutoCompressionJobId is { } jobId)
        {
            _autoCompressionRunner.Abort(jobId);
        }
    }

    public Task<string?> QueueCurrentJobAsync(bool startImmediately = false, QueueJobPreflightResult? preflight = null)
    {
        try
        {
            var hasRunningJob = GetRunningEncodingJobCount() >= GetMaxConcurrentEncodingJobCount();
            preflight ??= AnalyzeCurrentJobForQueue();
            if (!string.IsNullOrWhiteSpace(preflight.ValidationError))
            {
                return Task.FromResult<string?>(preflight.ValidationError);
            }

            if (preflight.RunningOutputConflict is not null)
            {
                return Task.FromResult<string?>(Texts.QueueOutputPathRunningConflictMessage(preflight.RunningOutputConflict.SourceFileName, preflight.BaseOutputPath));
            }

            var request = CreateDraftRequest(finalOutputPathOverride: preflight.FinalOutputPath);
            var job = new EncodingJobItemViewModel(
                request,
                Texts.Pick("正在生成实际执行命令...", "Resolving the actual command..."),
                CurrentLanguagePreference);

            Jobs.Add(job);
            SelectedJob = job;
            RaiseJobSummaryPropertyChanges();

            StatusText = preflight.IsOutputPathAutoRenamed
                ? Texts.JobQueuedWithAutoOutputNameStatus(
                    Path.GetFileName(request.SourcePath),
                    Path.GetFileName(request.OutputPath),
                    startImmediately,
                    hasRunningJob,
                    preflight.QueuedOutputConflictCount,
                    preflight.DiskOutputPathExists)
                : Texts.JobQueuedStatus(Path.GetFileName(request.SourcePath), startImmediately, hasRunningJob);
            _ = ResolveJobDisplayCommandAsync(job, request);

            if (startImmediately)
            {
                _ = ProcessQueueAfterUiRefreshAsync();
            }

            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }
    }

    public string? ValidateCurrentJobForQueue(out string? existingOutputPath)
    {
        existingOutputPath = null;

        try
        {
            var preflight = AnalyzeCurrentJobForQueue();
            if (!string.IsNullOrWhiteSpace(preflight.ValidationError))
            {
                return preflight.ValidationError;
            }

            if (preflight.RunningOutputConflict is not null)
            {
                return Texts.QueueOutputPathRunningConflictMessage(preflight.RunningOutputConflict.SourceFileName, preflight.BaseOutputPath);
            }

            existingOutputPath = preflight.DiskOutputPathExists ? preflight.BaseOutputPath : null;
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public QueueJobPreflightResult AnalyzeCurrentJobForQueue()
    {
        try
        {
            var baseRequest = CreateDraftRequest(uniquifyOutputPath: false);
            var finalOutputPath = ResolveUniqueOutputPath(baseRequest.OutputPath);
            var duplicateJob = Jobs.FirstOrDefault(job => IsDuplicateJobRequest(job.Request, baseRequest));
            var runningOutputConflict = Jobs.FirstOrDefault(job =>
                job.State == EncodingJobState.Running
                && AreSamePath(job.Request.OutputPath, baseRequest.OutputPath));
            var queuedOutputConflictCount = Jobs.Count(job =>
                job.State != EncodingJobState.Running
                && AreSamePath(job.Request.OutputPath, baseRequest.OutputPath));
            var diskOutputPathExists = File.Exists(baseRequest.OutputPath) || Directory.Exists(baseRequest.OutputPath);

            return new QueueJobPreflightResult(
                baseRequest.OutputPath,
                finalOutputPath,
                duplicateJob,
                runningOutputConflict,
                queuedOutputConflictCount,
                diskOutputPathExists,
                ValidationError: null);
        }
        catch (Exception ex)
        {
            return new QueueJobPreflightResult(
                string.Empty,
                string.Empty,
                DuplicateJob: null,
                RunningOutputConflict: null,
                QueuedOutputConflictCount: 0,
                DiskOutputPathExists: false,
                ex.Message);
        }
    }

    public Task CancelJobAsync(EncodingJobItemViewModel? job)
    {
        if (job is null)
        {
            return Task.CompletedTask;
        }

        if (job.State == EncodingJobState.Queued)
        {
            job.MarkCancelled(
                Texts.Pick("队列中的作业已取消", "Queued job cancelled"),
                Texts.Pick("作业尚未启动，已从执行队列中撤回。", "The job had not started and was removed from the execution queue."));
            RaiseJobStatePropertyChanges();
            StatusText = Texts.QueuedJobCancelledStatus(job.SourceFileName);
        }
        else if (job.State == EncodingJobState.Running)
        {
            job.RequestCancellation();
            _jobRunner.AbortJob(job.Request.JobId);
            StatusText = Texts.RunningJobCancellingStatus(job.SourceFileName);
        }

        return Task.CompletedTask;
    }

    public string? StartSelectedJobsNow()
    {
        var selectedJobs = NormalizeSelectedQueueJobs(_selectedQueueJobs).ToList();
        if (selectedJobs.Count == 0)
        {
            return Texts.NoSelectedJobsError;
        }

        var startableJobs = selectedJobs
            .Where(static job => job.CanStart)
            .ToList();
        if (startableJobs.Count == 0)
        {
            return Texts.BatchStartNoQueuedJobsError;
        }

        var startedCount = 0;
        foreach (var job in startableJobs)
        {
            if (GetRunningEncodingJobCount() >= GetMaxConcurrentEncodingJobCount())
            {
                break;
            }

            SelectedJob = job;
            _ = RunJobAsync(job);
            startedCount++;
        }

        if (startedCount == 0)
        {
            return Texts.ConcurrentEncodingLimitReached(GetMaxConcurrentEncodingJobCount());
        }

        StatusText = startedCount == startableJobs.Count
            ? Texts.BatchJobsStartedStatus(startedCount)
            : Texts.BatchJobsStartedPartialStatus(startedCount, startableJobs.Count, GetMaxConcurrentEncodingJobCount());
        RaiseJobStatePropertyChanges();
        return null;
    }

    public string? CancelSelectedJobs()
    {
        var selectedJobs = NormalizeSelectedQueueJobs(_selectedQueueJobs).ToList();
        if (selectedJobs.Count == 0)
        {
            return Texts.NoSelectedJobsError;
        }

        var cancelableJobs = selectedJobs
            .Where(static job => job.CanCancel)
            .ToList();
        if (cancelableJobs.Count == 0)
        {
            return Texts.BatchCancelNoCancelableJobsError;
        }

        var queuedCount = 0;
        var runningCount = 0;
        foreach (var job in cancelableJobs)
        {
            if (job.State == EncodingJobState.Queued)
            {
                queuedCount++;
            }
            else if (job.State == EncodingJobState.Running)
            {
                runningCount++;
            }

            _ = CancelJobAsync(job);
        }

        StatusText = Texts.BatchJobsCancelRequestedStatus(cancelableJobs.Count, runningCount, queuedCount);
        RaiseJobStatePropertyChanges();
        return null;
    }

    public string? RemoveSelectedJobs()
    {
        var selectedJobs = NormalizeSelectedQueueJobs(_selectedQueueJobs).ToList();
        if (selectedJobs.Count == 0)
        {
            return Texts.NoSelectedJobsError;
        }

        var removableJobs = selectedJobs
            .Where(static job => job.CanRemove)
            .ToList();
        if (removableJobs.Count == 0)
        {
            return Texts.BatchDeleteNoRemovableJobsError;
        }

        var removedCount = 0;
        foreach (var job in removableJobs)
        {
            if (Jobs.Remove(job))
            {
                removedCount++;
            }
        }

        PruneSelectedQueueJobs();
        if (SelectedJob is not null && !Jobs.Contains(SelectedJob))
        {
            SelectedJob = _selectedQueueJobs.FirstOrDefault()
                ?? Jobs.FirstOrDefault();
        }

        var skippedRunningCount = selectedJobs.Count - removableJobs.Count;
        RaiseJobSummaryPropertyChanges();
        StatusText = Texts.BatchJobsDeletedStatus(removedCount, skippedRunningCount);
        return null;
    }

    public Task<string?> RestartJobAsync(EncodingJobItemViewModel? job)
    {
        if (job is null)
        {
            return Task.FromResult<string?>(Texts.Pick("未找到要重启的任务。", "The job to restart was not found."));
        }

        if (!job.CanRestart)
        {
            return Task.FromResult<string?>(Texts.Pick("只有已完成、失败或已取消的任务才能重启。", "Only completed, failed, or cancelled jobs can be restarted."));
        }

        try
        {
            var request = job.Request with { JobId = Guid.NewGuid() };
            EnsureRequestConstraintsSatisfied(request);
            var restartedJob = new EncodingJobItemViewModel(
                request,
                Texts.Pick("正在生成实际执行命令...", "Resolving the actual command..."),
                CurrentLanguagePreference);

            Jobs.Add(restartedJob);
            SelectedJob = restartedJob;
            RaiseJobSummaryPropertyChanges();

            StatusText = Texts.JobRestartedStatus(restartedJob.SourceFileName);
            _ = ResolveJobDisplayCommandAsync(restartedJob, request);
            _ = ProcessQueueAfterUiRefreshAsync();
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>(ex.Message);
        }
    }

    public string? RemoveJob(EncodingJobItemViewModel? job)
    {
        if (job is null)
        {
            return Texts.RemoveJobMissingError;
        }

        if (!job.CanRemove)
        {
            return Texts.RemoveRunningJobError;
        }

        if (!Jobs.Remove(job))
        {
            return Texts.RemoveJobFailedError;
        }

        if (ReferenceEquals(SelectedJob, job))
        {
            SelectedJob = Jobs.FirstOrDefault();
        }

        PruneSelectedQueueJobs();
        RaiseJobSummaryPropertyChanges();
        StatusText = Texts.JobDeletedStatus(job.SourceFileName);
        return null;
    }

    public string? PrioritizeJob(EncodingJobItemViewModel? job)
    {
        var error = MoveQueuedJob(job, MoveQueuedJobMode.Next);
        if (string.IsNullOrWhiteSpace(error))
        {
            _ = ProcessQueueAsync();
        }

        return error;
    }

    public string? StartJobNow(EncodingJobItemViewModel? job)
    {
        if (job is null)
        {
            return Texts.StartJobMissingError;
        }

        if (!job.CanStart)
        {
            return Texts.StartJobInvalidError;
        }

        if (GetRunningEncodingJobCount() >= GetMaxConcurrentEncodingJobCount())
        {
            return Texts.ConcurrentEncodingLimitReached(GetMaxConcurrentEncodingJobCount());
        }

        SelectedJob = job;
        StatusText = Texts.JobStartedManuallyStatus(job.SourceFileName);
        _ = RunJobAsync(job);
        return null;
    }

    public string? MoveJobUp(EncodingJobItemViewModel? job)
    {
        return MoveQueuedJob(job, MoveQueuedJobMode.Up);
    }

    public string? MoveJobDown(EncodingJobItemViewModel? job)
    {
        return MoveQueuedJob(job, MoveQueuedJobMode.Down);
    }

    public string? MoveJobToTop(EncodingJobItemViewModel? job)
    {
        return MoveQueuedJob(job, MoveQueuedJobMode.Top);
    }

    public string? MoveJobToBottom(EncodingJobItemViewModel? job)
    {
        return MoveQueuedJob(job, MoveQueuedJobMode.Bottom);
    }

    private Task ProcessQueueAsync()
    {
        if (_isQueueProcessing || _isShuttingDown)
        {
            return Task.CompletedTask;
        }

        _isQueueProcessing = true;

        try
        {
            while (true)
            {
                if (_isShuttingDown)
                {
                    break;
                }

                if (GetRunningEncodingJobCount() >= GetMaxConcurrentEncodingJobCount())
                {
                    break;
                }

                var nextJob = Jobs.FirstOrDefault(static job => job.State == EncodingJobState.Queued);
                if (nextJob is null)
                {
                    break;
                }

                _ = RunJobAsync(nextJob);
            }
        }
        finally
        {
            _isQueueProcessing = false;
        }

        return Task.CompletedTask;
    }

    private async Task ProcessQueueAfterUiRefreshAsync()
    {
        await Task.Yield();
        await Task.Delay(50);
        await ProcessQueueAsync();
    }

    private int GetRunningEncodingJobCount()
    {
        return Jobs.Count(static job => job.State == EncodingJobState.Running);
    }

    private int GetMaxConcurrentEncodingJobCount()
    {
        return NormalizeConcurrentEncodingJobs(MaxConcurrentEncodingJobs);
    }

    private static int NormalizeConcurrentEncodingJobs(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return MinConcurrentEncodingJobs;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, MinConcurrentEncodingJobs, MaxConcurrentEncodingJobsLimit);
    }

    private void SyncSelectedConcurrentEncodingJobOption(int normalizedValue)
    {
        var targetValue = normalizedValue.ToString(CultureInfo.InvariantCulture);
        var matchedOption = ConcurrentEncodingJobOptions.FirstOrDefault(option =>
            string.Equals(option.Value, targetValue, StringComparison.Ordinal));

        if (ReferenceEquals(_selectedConcurrentEncodingJobOption, matchedOption))
        {
            return;
        }

        _selectedConcurrentEncodingJobOption = matchedOption;
        OnPropertyChanged(nameof(SelectedConcurrentEncodingJobOption));
    }

    private void SyncSelectedQueueCompletionActionOption(QueueCompletionAction action)
    {
        var targetValue = action.ToString();
        var matchedOption = QueueCompletionActionOptions.FirstOrDefault(option =>
            string.Equals(option.Value, targetValue, StringComparison.Ordinal));

        if (ReferenceEquals(_selectedQueueCompletionActionOption, matchedOption))
        {
            return;
        }

        _selectedQueueCompletionActionOption = matchedOption;
        OnPropertyChanged(nameof(SelectedQueueCompletionActionOption));
    }

    private static bool IsPendingQueueWork(EncodingJobItemViewModel job)
    {
        return job.State is EncodingJobState.Queued or EncodingJobState.Running;
    }

    private bool HasPendingQueueWork()
    {
        return Jobs.Any(IsPendingQueueWork);
    }

    private bool HasActiveAppWork()
    {
        return HasPendingQueueWork()
            || IsAutoCompressionRunning
            || IsAudioProcessingRunning
            || IsBluRayDemuxRunning;
    }

    private void ResetQueueCompletionActionBatch()
    {
        _isQueueCompletionActionArmed = false;
        _queueCompletionActionBatchHadNonSuccessfulCompletion = false;
    }

    private void BeginQueueCompletionActionBatch()
    {
        CancelPendingQueueCompletionActionWait();
        _isQueueCompletionActionArmed = QueueCompletionAction != QueueCompletionAction.None;
        _queueCompletionActionBatchHadNonSuccessfulCompletion = false;
    }

    private void MarkQueueCompletionActionBatchNonSuccessful()
    {
        if (!_isQueueCompletionActionArmed)
        {
            return;
        }

        _queueCompletionActionBatchHadNonSuccessfulCompletion = true;
        CancelPendingQueueCompletionActionWait();
    }

    private bool ShouldTreatNonSuccessfulJobAsUnattendedFailure()
    {
        return _isQueueCompletionActionArmed
            && _systemIdleService.GetIdleDuration() >= QueueCompletionActionIdleRequirement;
    }

    private void CancelPendingQueueCompletionActionWait()
    {
        _queueCompletionActionWaitCancellationTokenSource?.Cancel();
        _queueCompletionActionWaitCancellationTokenSource?.Dispose();
        _queueCompletionActionWaitCancellationTokenSource = null;
    }

    private void TryScheduleQueueCompletionActionAfterSuccessfulQueueDrain()
    {
        if (_isShuttingDown || _isExecutingQueueCompletionAction || !_isQueueCompletionActionArmed)
        {
            return;
        }

        if (_queueCompletionActionBatchHadNonSuccessfulCompletion)
        {
            ResetQueueCompletionActionBatch();
            return;
        }

        if (HasActiveAppWork())
        {
            return;
        }

        var action = QueueCompletionAction;
        if (action == QueueCompletionAction.None)
        {
            ResetQueueCompletionActionBatch();
            return;
        }

        CancelPendingQueueCompletionActionWait();
        var cancellationTokenSource = new CancellationTokenSource();
        _queueCompletionActionWaitCancellationTokenSource = cancellationTokenSource;
        ResetQueueCompletionActionBatch();
        StatusText = Texts.QueueCompletionActionPendingIdleStatus(action, QueueCompletionActionIdleRequirement);
        _ = WaitForQueueCompletionActionIdleAndExecuteAsync(cancellationTokenSource);
    }

    private async Task WaitForQueueCompletionActionIdleAndExecuteAsync(CancellationTokenSource cancellationTokenSource)
    {
        var cancellationToken = cancellationTokenSource.Token;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_isShuttingDown || _isExecutingQueueCompletionAction || HasActiveAppWork())
                {
                    return;
                }

                var action = QueueCompletionAction;
                if (action == QueueCompletionAction.None)
                {
                    return;
                }

                if (_systemIdleService.GetIdleDuration() >= QueueCompletionActionIdleRequirement)
                {
                    await ExecuteQueueCompletionActionAsync(action);
                    return;
                }

                await Task.Delay(QueueCompletionActionIdlePollInterval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_queueCompletionActionWaitCancellationTokenSource, cancellationTokenSource))
            {
                _queueCompletionActionWaitCancellationTokenSource = null;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private async Task ExecuteQueueCompletionActionAsync(QueueCompletionAction action)
    {
        _isExecutingQueueCompletionAction = true;
        try
        {
            StatusText = Texts.QueueCompletionActionExecutingStatus(action);
            var error = await _queueCompletionActionService.ExecuteAsync(action);
            if (!string.IsNullOrWhiteSpace(error))
            {
                StatusText = Texts.QueueCompletionActionFailedStatus(action, error);
            }
        }
        finally
        {
            _isExecutingQueueCompletionAction = false;
        }
    }

    private async Task RunJobAsync(EncodingJobItemViewModel job)
    {
        if (job.State != EncodingJobState.Queued || _isShuttingDown)
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource();
        if (QueueCompletionAction != QueueCompletionAction.None && GetRunningEncodingJobCount() == 0)
        {
            BeginQueueCompletionActionBatch();
        }

        job.AttachCancellation(cancellationSource);
        job.MarkRunning();
        RaiseJobStatePropertyChanges();

        try
        {
            StatusText = Texts.EncodingStartedStatus(job.SourceFileName);
            if (SelectedJob is null)
            {
                SelectedJob = job;
            }

            var progress = new Progress<EncodingJobProgress>(update =>
            {
                var previousState = job.State;
                job.ApplyProgress(update);
                if (previousState != job.State)
                {
                    RaiseJobStatePropertyChanges();
                }
            });

            var result = await Task.Run(
                () => _jobRunner.RunAsync(job.Request, progress, cancellationSource.Token),
                cancellationSource.Token);
            var previousState = job.State;
            job.ApplyResult(result);
            if (previousState != job.State)
            {
                RaiseJobStatePropertyChanges();
            }

            if (result.State is EncodingJobState.Failed or EncodingJobState.Cancelled)
            {
                if (ShouldTreatNonSuccessfulJobAsUnattendedFailure())
                {
                    MarkQueueCompletionActionBatchNonSuccessful();
                }
            }

            StatusText = Texts.EncodingFinishedStatus(job.SourceFileName, result.Summary);
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            if (ShouldTreatNonSuccessfulJobAsUnattendedFailure())
            {
                MarkQueueCompletionActionBatchNonSuccessful();
            }

            job.MarkCancelled(
                Texts.Pick("编码已取消", "Encoding cancelled"),
                Texts.Pick("作业被用户中断。", "The job was cancelled by the user."));
            RaiseJobStatePropertyChanges();
            StatusText = Texts.EncodingCancelledStatus(job.SourceFileName);
        }
        catch (Exception ex)
        {
            if (ShouldTreatNonSuccessfulJobAsUnattendedFailure())
            {
                MarkQueueCompletionActionBatchNonSuccessful();
            }

            job.MarkFailed(Texts.Pick($"编码失败：{ex.Message}", $"Encoding failed: {ex.Message}"), ex.ToString());
            RaiseJobStatePropertyChanges();
            StatusText = Texts.EncodingFailedStatus(job.SourceFileName);
        }
        finally
        {
            job.DetachCancellation();
            _ = ProcessQueueAsync();
            TryScheduleQueueCompletionActionAfterSuccessfulQueueDrain();
        }
    }

    public async Task CancelRunningJobsForShutdownAsync()
    {
        _isShuttingDown = true;
        CancelPendingQueueCompletionActionWait();
        ResetQueueCompletionActionBatch();
        CancelAutoCompression();
        CancelAudioProcessing();
        CancelBluRayDemux();

        var runningJobs = Jobs
            .Where(static job => job.State == EncodingJobState.Running)
            .ToList();

        if (runningJobs.Count == 0 && !IsAutoCompressionRunning && !IsAudioProcessingRunning && !IsBluRayDemuxRunning)
        {
            return;
        }

        StatusText = Texts.ShuttingDownStatus(runningJobs.Count, IsAutoCompressionRunning, IsAudioProcessingRunning, IsBluRayDemuxRunning);

        foreach (var job in runningJobs)
        {
            job.RequestCancellation();
            _jobRunner.AbortJob(job.Request.JobId);
        }

        var timeoutAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while ((runningJobs.Any(static job => job.State == EncodingJobState.Running)
                || IsAutoCompressionRunning
                || IsAudioProcessingRunning
                || IsBluRayDemuxRunning)
               && DateTimeOffset.UtcNow < timeoutAt)
        {
            await Task.Delay(100);
        }
    }

    private string? MoveQueuedJob(EncodingJobItemViewModel? job, MoveQueuedJobMode mode)
    {
        if (job is null)
        {
            return Texts.MoveJobMissingError;
        }

        if (job.State != EncodingJobState.Queued)
        {
            return Texts.MoveJobInvalidError;
        }

        var currentIndex = Jobs.IndexOf(job);
        if (currentIndex < 0)
        {
            return Texts.MoveJobNotInQueueError;
        }

        var minimumIndex = GetQueuedMoveFloorIndex();
        var maximumIndex = Jobs.Count - 1;
        var targetIndex = mode switch
        {
            MoveQueuedJobMode.Next or MoveQueuedJobMode.Top => minimumIndex,
            MoveQueuedJobMode.Up => Math.Max(minimumIndex, currentIndex - 1),
            MoveQueuedJobMode.Down => Math.Min(maximumIndex, currentIndex + 1),
            MoveQueuedJobMode.Bottom => maximumIndex,
            _ => currentIndex
        };

        if (targetIndex == currentIndex)
        {
            StatusText = Texts.MoveJobEdgeStatus(mode, job.SourceFileName);

            return null;
        }

        Jobs.Move(currentIndex, targetIndex);
        SelectedJob = job;
        RaiseJobSummaryPropertyChanges();

        StatusText = Texts.MoveJobCompletedStatus(mode, job.SourceFileName);

        return null;
    }

    private int GetQueuedMoveFloorIndex()
    {
        var runningIndex = Jobs
            .Select(static (job, index) => job.State == EncodingJobState.Running ? (int?)index : null)
            .Max();

        return runningIndex.HasValue ? runningIndex.Value + 1 : 0;
    }

    private AutoCompressionRequest CreateAutoCompressionRequest(bool requireSourceExists)
    {
        if (SelectedAutoEncoder is null)
        {
            throw new InvalidOperationException(Texts.AutoCompressionMissingEncoderError);
        }

        if (string.IsNullOrWhiteSpace(AutoCompressionSourcePath))
        {
            throw new InvalidOperationException(Texts.AutoCompressionMissingSourceError);
        }

        if (string.IsNullOrWhiteSpace(AutoCompressionOutputPath))
        {
            throw new InvalidOperationException(Texts.AutoCompressionMissingOutputError);
        }

        var normalizedSource = Path.GetFullPath(AutoCompressionSourcePath.Trim());
        var normalizedOutputDirectory = Path.GetFullPath(AutoCompressionOutputPath.Trim());

        if (requireSourceExists && !File.Exists(normalizedSource))
        {
            throw new FileNotFoundException(Texts.AutoCompressionSourceFileMissingError, normalizedSource);
        }

        if (requireSourceExists && File.Exists(normalizedOutputDirectory))
        {
            throw new InvalidOperationException(Texts.AutoCompressionOutputDirectoryInvalidError);
        }

        var normalizedOutput = ResolveAutoCompressionOutputPath(
            normalizedSource,
            normalizedOutputDirectory,
            SelectedAutoEncoder.Value,
            AutoCompressionTargetVmaf);

        if (string.Equals(normalizedSource, normalizedOutput, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Texts.AutoCompressionSourceOutputPathConflictError);
        }

        var probes = Math.Max(1, (int)Math.Round(AutoCompressionProbes, MidpointRounding.AwayFromZero));
        var workers = AutoCompressionWorkers > 0
            ? (int?)Math.Round(AutoCompressionWorkers, MidpointRounding.AwayFromZero)
            : null;
        return new AutoCompressionRequest(
            Guid.NewGuid(),
            normalizedSource,
            normalizedOutput,
            SelectedAutoEncoder.Value,
            AutoCompressionTargetVmaf,
            probes,
            AutoCompressionVideoParameters.Trim(),
            workers);
    }

    private bool TryCreateAutoCompressionRequest(
        bool requireSourceExists,
        out AutoCompressionRequest? request,
        out string? error)
    {
        try
        {
            request = CreateAutoCompressionRequest(requireSourceExists);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            request = null;
            error = ex.Message;
            return false;
        }
    }

    private void RefreshAutoCompressionCommandPreview()
    {
        if (_isAutoCompressionRunning)
        {
            return;
        }

        AutoCompressionCommandLine = TryCreateAutoCompressionRequest(requireSourceExists: false, out var request, out _)
            ? _autoCompressionRunner.BuildDisplayCommand(request!)
            : string.Empty;
    }

    private EncodingJobRequest CreateDraftRequest(
        bool requireSourceExists = true,
        bool uniquifyOutputPath = true,
        string? finalOutputPathOverride = null)
    {
        if (_activeProfile is null)
        {
            throw new InvalidOperationException(Texts.MissingEncoderError);
        }

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            throw new InvalidOperationException(Texts.MissingSourceError);
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            throw new InvalidOperationException(Texts.MissingOutputError);
        }

        var normalizedSource = Path.GetFullPath(SourcePath.Trim());
        var normalizedOutputDirectory = Path.GetFullPath(OutputPath.Trim());

        if (requireSourceExists && !File.Exists(normalizedSource))
        {
            throw new FileNotFoundException(Texts.SourceFileMissingError, normalizedSource);
        }

        if (requireSourceExists && File.Exists(normalizedOutputDirectory))
        {
            throw new InvalidOperationException(Texts.OutputDirectoryInvalidError);
        }

        var normalizedOutput = string.IsNullOrWhiteSpace(finalOutputPathOverride)
            ? ResolveDraftOutputPath(
                normalizedSource,
                normalizedOutputDirectory,
                _activeProfile)
            : Path.GetFullPath(finalOutputPathOverride.Trim());
        if (uniquifyOutputPath && string.IsNullOrWhiteSpace(finalOutputPathOverride))
        {
            normalizedOutput = ResolveUniqueOutputPath(normalizedOutput);
        }

        if (string.Equals(normalizedSource, normalizedOutput, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Texts.SourceOutputPathConflictError);
        }

        var request = new EncodingJobRequest(
            Guid.NewGuid(),
            _activeProfile,
            normalizedSource,
            normalizedOutput,
            InputSourceSupport.ResolvePipelineKind(normalizedSource),
            EncoderArchitecture.X64);

        EnsureRequestConstraintsSatisfied(request);
        return request;
    }

    private static string ResolveDraftOutputPath(string sourcePath, string outputDirectory, EncodingProfile? profile)
    {
        var fileName = BuildDraftOutputBaseFileName(sourcePath, profile);

        var extension = string.IsNullOrWhiteSpace(profile?.OutputContainer)
            ? "264"
            : profile.OutputContainer.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "264";
        }

        return Path.Combine(outputDirectory, $"{fileName}.{extension}");
    }

    private static string BuildDraftOutputBaseFileName(string sourcePath, EncodingProfile? profile)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "encode";
        }

        if (profile?.RateControl != RateControlMode.Crf)
        {
            return fileName;
        }

        return $"{fileName}-{GetDraftOutputEncoderToken(profile.Kind)}-crf{FormatOutputTokenNumber(profile.Quality)}";
    }

    private static string GetDraftOutputEncoderToken(EncoderKind kind)
    {
        return kind switch
        {
            EncoderKind.X264 => "x264",
            EncoderKind.X265 => "x265",
            EncoderKind.SvtAv1 => "svtav1",
            _ => kind.ToString().ToLowerInvariant()
        };
    }

    private static string FormatOutputTokenNumber(double value)
    {
        return value
            .ToString("0.0##", CultureInfo.InvariantCulture)
            .TrimEnd('0')
            .TrimEnd('.');
    }

    private string ResolveUniqueOutputPath(string outputPath)
    {
        var normalizedOutputPath = Path.GetFullPath(outputPath.Trim());
        if (!IsOutputPathOccupied(normalizedOutputPath))
        {
            return normalizedOutputPath;
        }

        var directory = Path.GetDirectoryName(normalizedOutputPath) ?? Environment.CurrentDirectory;
        var extension = Path.GetExtension(normalizedOutputPath);
        var baseName = Path.GetFileNameWithoutExtension(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "encode";
        }

        for (var index = 1; index < 10000; index++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({index.ToString(CultureInfo.InvariantCulture)}){extension}");
            if (!IsOutputPathOccupied(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{baseName} ({Guid.NewGuid():N}){extension}");
    }

    private bool IsOutputPathOccupied(string outputPath)
    {
        return File.Exists(outputPath)
            || Directory.Exists(outputPath)
            || Jobs.Any(job => AreSamePath(job.Request.OutputPath, outputPath));
    }

    private static bool IsDuplicateJobRequest(EncodingJobRequest left, EncodingJobRequest right)
    {
        return AreSamePath(left.SourcePath, right.SourcePath)
            && AreSamePath(left.OutputPath, right.OutputPath)
            && left.PipelineKind == right.PipelineKind
            && left.PreferredArchitecture == right.PreferredArchitecture
            && EqualityComparer<EncodingProfile>.Default.Equals(left.Profile, right.Profile);
    }

    private static bool AreSamePath(string leftPath, string rightPath)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(leftPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(rightPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ResolveAutoCompressionOutputPath(
        string sourcePath,
        string outputDirectory,
        EncoderKind encoderKind,
        double targetVmaf)
    {
        var fileName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "encode";
        }

        return Path.Combine(
            outputDirectory,
            $"{fileName}.{GetAutoCompressionEncoderToken(encoderKind)}.vmaf{FormatAutoCompressionVmafToken(targetVmaf)}.mkv");
    }

    private string? TryResolveDraftOutputPreviewPath()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SourcePath))
            {
                return null;
            }

            var normalizedSource = Path.GetFullPath(SourcePath.Trim());
            var outputDirectory = !string.IsNullOrWhiteSpace(OutputPath)
                ? Path.GetFullPath(OutputPath.Trim())
                : Path.GetDirectoryName(normalizedSource) ?? Environment.CurrentDirectory;
            return ResolveUniqueOutputPath(ResolveDraftOutputPath(normalizedSource, outputDirectory, _activeProfile));
        }
        catch
        {
            return null;
        }
    }

    private string? TryResolveAutoCompressionOutputPreviewPath()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AutoCompressionSourcePath) || SelectedAutoEncoder is null)
            {
                return null;
            }

            var normalizedSource = Path.GetFullPath(AutoCompressionSourcePath.Trim());
            var outputDirectory = !string.IsNullOrWhiteSpace(AutoCompressionOutputPath)
                ? Path.GetFullPath(AutoCompressionOutputPath.Trim())
                : Path.GetDirectoryName(normalizedSource) ?? Environment.CurrentDirectory;
            return ResolveAutoCompressionOutputPath(
                normalizedSource,
                outputDirectory,
                SelectedAutoEncoder.Value,
                AutoCompressionTargetVmaf);
        }
        catch
        {
            return null;
        }
    }

    private string BuildOutputPreviewText(string? outputPath)
    {
        return string.IsNullOrWhiteSpace(outputPath)
            ? Texts.OutputPreviewPlaceholder
            : Texts.OutputPreviewText(outputPath);
    }

    private static string GetAutoCompressionEncoderToken(EncoderKind encoderKind)
    {
        return encoderKind switch
        {
            EncoderKind.X264 => "x264",
            EncoderKind.X265 => "x265",
            EncoderKind.SvtAv1 => "av1",
            _ => "encode"
        };
    }

    private static string FormatAutoCompressionVmafToken(double targetVmaf)
    {
        var token = Math.Clamp(targetVmaf, 0, 100).ToString("0.###", CultureInfo.InvariantCulture);
        return token.Replace(".", "p", StringComparison.Ordinal);
    }

    private string? GetProfileConstraintError(EncodingProfile? profile)
    {
        if (profile is null)
        {
            return null;
        }

        if (SvtAv1ProfileConstraints.HasTwoPassOverlayConflict(profile))
        {
            return Texts.SvtAv1TwoPassOverlayConflict;
        }

        return GetArgumentConflictError(profile.Kind, profile.AdditionalArguments, profile.UhdParameters);
    }

    private string? GetRequestConstraintError(EncodingJobRequest request)
    {
        if (SvtAv1ProfileConstraints.HasTwoPassOverlayConflict(request.Profile))
        {
            return Texts.SvtAv1TwoPassOverlayConflict;
        }

        return GetArgumentConflictError(
            request.Profile.Kind,
            request.Profile.AdditionalArguments,
            request.Profile.UhdParameters);
    }

    private string? GetArgumentConflictError(
        EncoderKind kind,
        string? additionalArguments,
        string? uhdParameters)
    {
        var argumentConflict = EncoderArgumentConflictValidator.FindFirstConflict(
            kind,
            additionalArguments,
            uhdParameters);
        if (argumentConflict is not null)
        {
            return Texts.DescribeArgumentConflict(argumentConflict);
        }

        return null;
    }

    private void EnsureRequestConstraintsSatisfied(EncodingJobRequest request)
    {
        var error = GetRequestConstraintError(request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }
    }

    private async Task RefreshPreviewNowAsync(EncodingProfile profile)
    {
        CancelPendingPreviewRefresh();
        var requestVersion = Interlocked.Increment(ref _previewRefreshVersion);
        await UpdatePreviewAsync(profile, requestVersion, CancellationToken.None);
    }

    private async Task UpdatePreviewAsync(
        EncodingProfile profile,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        var preview = await _profileLibraryService.BuildPreviewAsync(profile, cancellationToken);
        if (!IsPreviewRequestCurrent(requestVersion, cancellationToken))
        {
            return;
        }

        PreviewTitle = Texts.PipelinePreviewTitle(profile.Name, profile.Kind);
        PreviewCommandLine = preview.CommandLine;
        PreviewNotes = Texts.PipelinePreviewNotes(profile.Kind);

        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(OutputPath))
        {
            return;
        }

        try
        {
            var request = CreateDraftRequest(requireSourceExists: false);
            if (!IsPreviewRequestCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            var displayCommand = await BuildDisplayCommandAsync(request, cancellationToken);
            var resolvedNotes = BuildResolvedPreviewNotes(request);
            if (!IsPreviewRequestCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            PreviewTitle = Texts.ActualCommandTitle(profile.Name);
            PreviewCommandLine = displayCommand;
            PreviewNotes = resolvedNotes;
        }
        catch (Exception ex)
        {
            if (!IsPreviewRequestCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            PreviewNotes = $"{Texts.PipelinePreviewNotes(profile.Kind)}{Environment.NewLine}{Environment.NewLine}{Texts.ActualDraftNotReadyMessage(ex.Message)}";
        }
    }

    private string BuildResolvedPreviewNotes(EncodingJobRequest request)
    {
        var resolvedBinary = ResolveEncoderFromCachedSources(
            request.Profile.Kind,
            request.PreferredArchitecture);

        var binarySummary = resolvedBinary is null
            ? Texts.ResolvedBinaryMissing
            : Texts.ResolvedBinarySummary(
                BuildBinarySourceSummary(resolvedBinary),
                Path.GetFileName(resolvedBinary.ExecutablePath));

        return Texts.ResolvedPreviewNotes(request.OutputPath, binarySummary);
    }

    private void RefreshEncoderOptions()
    {
        var currentKind = SelectedEncoder?.Value ?? _activeProfile?.Kind ?? EncoderKind.X264;
        var autoKind = SelectedAutoEncoder?.Value ?? currentKind;
        var source = Encoders.Count == 0
            ? Enum.GetValues<EncoderKind>().Select(kind => new EncoderOption(kind, kind.ToDisplayName()))
            : Encoders.Select(item => new EncoderOption(item.Capability.Kind, item.Capability.DisplayName));

        _isSynchronizingDraft = true;

        try
        {
            ReplaceItems(EncoderOptions, source);
            SelectedEncoder = EncoderOptions.FirstOrDefault(option => option.Value == currentKind) ?? EncoderOptions.FirstOrDefault();
            SelectedAutoEncoder = EncoderOptions.FirstOrDefault(option => option.Value == autoKind) ?? EncoderOptions.FirstOrDefault();
        }
        finally
        {
            _isSynchronizingDraft = false;
        }
    }

    private void RefreshTemplateLibraryItems()
    {
        IEnumerable<TemplateLibraryItemViewModel> items =
            UserTemplates.Select(BuildUserTemplateLibraryItem);

        var search = TemplateSearchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            items = items.Where(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceItems(TemplateLibraryItems, items);
        OnPropertyChanged(nameof(TemplateLibraryEmptyVisibility));
    }

    private void CaptureTemplateEditingBaseline(
        string? editingTemplateId,
        string? selectionKey,
        string templateName,
        string templateNotes,
        EncodingProfile? profile)
    {
        _editingTemplateId = editingTemplateId;
        _currentTemplateSelectionKey = selectionKey;
        _templateBaselineName = templateName?.Trim() ?? string.Empty;
        _templateBaselineNotes = templateNotes?.Trim() ?? string.Empty;
        _templateBaselineProfile = profile;

        OnPropertyChanged(nameof(EditingUserTemplateId));
        OnPropertyChanged(nameof(CurrentTemplateSelectionKey));
        OnPropertyChanged(nameof(HasUnsavedTemplateChanges));
        RaiseTemplateLockPropertyChanges();
    }

    private SavedTemplate? BuildDraftTemplateForExchange()
    {
        if (_activeProfile is null)
        {
            return null;
        }

        var normalizedTemplateName = DraftTemplateName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTemplateName))
        {
            return null;
        }

        var normalizedTemplateNotes = DraftTemplateNotes?.Trim() ?? string.Empty;
        return new SavedTemplate(
            _editingTemplateId ?? Guid.NewGuid().ToString("N"),
            normalizedTemplateName,
            normalizedTemplateNotes,
            _activeProfile with
            {
                Name = normalizedTemplateName,
                Description = normalizedTemplateNotes
            },
            DateTimeOffset.Now);
    }

    private async Task<SavedTemplate> PersistUserTemplateAsync(
        string templateName,
        string templateNotes,
        EncodingProfile profile,
        string? templateId,
        bool isPinned,
        string statusText)
    {
        var savedTemplate = await _profileLibraryService.SaveTemplateAsync(
            templateName,
            templateNotes,
            profile,
            templateId,
            isPinned);

        _isSynchronizingDraft = true;

        try
        {
            _draftProfileName = savedTemplate.Profile.Name;
            _draftProfileDescription = savedTemplate.Profile.Description;
            _activeProfile = savedTemplate.Profile;
            DraftTemplateName = savedTemplate.Name;
            DraftTemplateNotes = savedTemplate.Notes;
        }
        finally
        {
            _isSynchronizingDraft = false;
        }

        ReplaceItems(UserTemplates, await _profileLibraryService.GetUserTemplatesAsync());
        RefreshTemplateLibraryItems();
        CaptureTemplateEditingBaseline(
            savedTemplate.Id,
            $"user:{savedTemplate.Id}",
            savedTemplate.Name,
            savedTemplate.Notes,
            savedTemplate.Profile);
        SelectedProfileCaption = Texts.UserCaption(savedTemplate.Name);
        RaiseSummaryPropertyChanges();
        StatusText = statusText;
        return savedTemplate;
    }

    private bool MatchesTemplateEditingBaseline()
    {
        var currentName = DraftTemplateName?.Trim() ?? string.Empty;
        var currentNotes = DraftTemplateNotes?.Trim() ?? string.Empty;

        return string.Equals(currentName, _templateBaselineName, StringComparison.Ordinal)
            && string.Equals(currentNotes, _templateBaselineNotes, StringComparison.Ordinal)
            && EqualityComparer<EncodingProfile?>.Default.Equals(_activeProfile, _templateBaselineProfile);
    }

    private TemplateLibraryItemViewModel BuildUserTemplateLibraryItem(SavedTemplate template)
    {
        return new TemplateLibraryItemViewModel(
            $"user:{template.Id}",
            template.Name,
            template.IsPinned ? Texts.TemplateSourcePinned : Texts.TemplateSourceUser,
            $"{template.Profile.EncoderLabel} · {template.Profile.QualitySummary}",
            template.UpdatedLabel,
            template.Id,
            template.IsPinned,
            template.IsPinned ? Texts.UnpinTemplateButton : Texts.PinTemplateButton,
            template,
            ResolveBrush("TemplateUserCardBrush"),
            ResolveBrush("TemplateUserBorderBrush"),
            ResolveBrush("TemplateUserBadgeBrush"),
            ResolveBrush("TemplateUserBadgeForegroundBrush"));
    }

    private SavedTemplate? GetEditingUserTemplate()
    {
        return FindUserTemplateById(_editingTemplateId);
    }

    private static Brush ResolveBrush(string key)
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Colors.Transparent);
    }

    private static Brush ResolveTaskStatusPanelBorderBrush(EncodingJobState? state)
    {
        return state switch
        {
            EncodingJobState.Failed => ResolveBrush("AppErrorBrush"),
            EncodingJobState.Cancelled => ResolveBrush("AppNeutralBrush"),
            _ => ResolveBrush("CardBorderBrush")
        };
    }

    private void ApplyEnvironmentReadiness(EnvironmentReadinessReport report)
    {
        _environmentReadinessReport = report;
        _setupGuideLocalCheckedAt = report.CheckedAt;
        RaiseSetupGuidePropertyChanges();
        HandleAudioEnvironmentReadinessApplied();
        HandleBluRayEnvironmentReadinessApplied();
    }

    private string BuildRequirementLabel(CapabilityRequirementReadiness requirement)
    {
        return string.Join(
            " / ",
            requirement.CandidateResults.Select(static result => result.DisplayName));
    }

    private string BuildRequirementDetail(CapabilityRequirementReadiness requirement)
    {
        var label = BuildRequirementLabel(requirement);
        var preferredCandidate = requirement.CandidateResults.FirstOrDefault(static candidate => candidate.IsReady)
            ?? requirement.CandidateResults.FirstOrDefault(static candidate => candidate.State == ReadinessState.Misconfigured)
            ?? requirement.CandidateResults.First();

        var detail = preferredCandidate.State switch
        {
            ReadinessState.Missing => Texts.ToolMissingDetail(label),
            ReadinessState.Unknown => Texts.ToolUnknownDetail(label),
            _ => BuildToolProbeDetail(preferredCandidate)
        };

        return $"{label} · {Texts.ReadinessStateLabel(preferredCandidate.State)} · {detail}";
    }

    private string BuildToolProbeDetail(ToolProbeResult result)
    {
        return result.State switch
        {
            ReadinessState.Ready when !string.IsNullOrWhiteSpace(result.DetectedVersion) =>
                $"{Texts.ToolDetectionSourceLabel(result.Source, result.SourceLabel)} · {result.DetectedVersion}",
            ReadinessState.Ready => Texts.ToolDetectionSourceLabel(result.Source, result.SourceLabel),
            ReadinessState.Misconfigured when !string.IsNullOrWhiteSpace(result.FailureReason) => result.FailureReason,
            ReadinessState.Missing => Texts.ToolMissingDetail(result.DisplayName),
            ReadinessState.Unknown when !string.IsNullOrWhiteSpace(result.FailureReason) => result.FailureReason,
            _ => Texts.ToolUnknownDetail(result.DisplayName)
        };
    }

    private void ApplyProfileToDraft(
        EncodingProfile profile,
        string sourceCaption,
        string templateName,
        string templateNotes)
    {
        _isSynchronizingDraft = true;

        try
        {
            _draftProfileName = profile.Name;
            _draftProfileDescription = profile.Description;
            DraftTemplateName = templateName;
            DraftTemplateNotes = templateNotes;
            SelectedProfileCaption = sourceCaption;
            SelectedEncoder = EncoderOptions.FirstOrDefault(option => option.Value == profile.Kind) ?? EncoderOptions.FirstOrDefault();
            ApplyCapabilityDefaults(profile);
        }
        finally
        {
            _isSynchronizingDraft = false;
        }

        FinalizeDraftChange(syncOutputPath: true, markAsCustomized: false);
    }

    private void ApplyCapabilityDefaults(EncodingProfile? preferredProfile = null)
    {
        var capability = GetSelectedCapability();
        if (capability is null)
        {
            _activeProfile = preferredProfile;
            RaiseComposerPropertyChanges();
            return;
        }

        var wasSynchronizingDraft = _isSynchronizingDraft;
        var baselineProfile = preferredProfile
            ?? DefaultEncodingProfiles.GetDefault(capability.Kind);
        _isSynchronizingDraft = true;

        try
        {
            _draftProfileName = baselineProfile.Name;
            _draftProfileDescription = baselineProfile.Description;

            ReplaceItems(
                AvailableRateControlModes,
                capability.RateControlModes.Select(mode => new RateControlOption(mode, mode.ToDisplayLabel())));
            ReplaceItems(
                AvailablePresets,
                capability.Presets.Select(preset => new StringChoiceOption(preset, preset)));
            ReplaceItems(AvailableTunes, BuildChoiceOptions(capability.Tunes, Texts.Pick("不指定", "None")));
            ReplaceItems(AvailableProfiles, BuildChoiceOptions(capability.Profiles, Texts.Pick("自动", "Auto")));
            ReplaceItems(
                AvailableOutputFormats,
                capability.OutputFormats.Select(format => new StringChoiceOption(format, $".{format}")));

            SelectedRateControl = AvailableRateControlModes.FirstOrDefault(option => option.Value == baselineProfile.RateControl)
                ?? AvailableRateControlModes.FirstOrDefault();
            SelectedPreset = FindChoiceOption(AvailablePresets, baselineProfile.Preset, fallbackToFirst: true);
            SelectedTune = FindChoiceOption(AvailableTunes, baselineProfile.Tune, fallbackToFirst: true);
            SelectedProfileOption = FindChoiceOption(AvailableProfiles, baselineProfile.Profile, fallbackToFirst: true);
            SelectedOutputFormat = FindChoiceOption(AvailableOutputFormats, baselineProfile.OutputContainer, fallbackToFirst: true);
            DraftQuality = baselineProfile.Quality;
            DraftBitrate = baselineProfile.Bitrate ?? 3500;
            DraftAdditionalArguments = baselineProfile.AdditionalArguments;
            DraftUhdParameters = baselineProfile.UhdParameters;
        }
        finally
        {
            _isSynchronizingDraft = wasSynchronizingDraft;
        }
    }

    private void ApplyManualArgumentOverrides(string rawArguments)
    {
        if (SelectedEncoder is null || string.IsNullOrWhiteSpace(rawArguments))
        {
            return;
        }

        var overrides = EncoderArgumentOverrideParser.Parse(SelectedEncoder.Value, rawArguments);
        var wasSynchronizingDraft = _isSynchronizingDraft;
        _isSynchronizingDraft = true;

        try
        {
            if (overrides.RateControl is { } rateControl)
            {
                SelectedRateControl = AvailableRateControlModes.FirstOrDefault(option => option.Value == rateControl) ?? SelectedRateControl;
            }

            if (overrides.Preset is not null)
            {
                SelectedPreset = FindChoiceOption(AvailablePresets, overrides.Preset, fallbackToFirst: false) ?? SelectedPreset;
            }

            if (overrides.Tune is not null)
            {
                SelectedTune = FindChoiceOption(AvailableTunes, overrides.Tune, fallbackToFirst: false) ?? SelectedTune;
            }

            if (overrides.Profile is not null)
            {
                SelectedProfileOption = FindChoiceOption(AvailableProfiles, overrides.Profile, fallbackToFirst: false) ?? SelectedProfileOption;
            }

            if (overrides.Quality is { } quality && quality > 0)
            {
                DraftQuality = quality;
            }

            if (overrides.Bitrate is { } bitrate && bitrate > 0)
            {
                DraftBitrate = bitrate;
            }
        }
        finally
        {
            _isSynchronizingDraft = wasSynchronizingDraft;
        }
    }

    private void FinalizeDraftChange(bool syncOutputPath, bool markAsCustomized)
    {
        _activeProfile = BuildCurrentDraftProfile();

        if (markAsCustomized)
        {
            SelectedProfileCaption = Texts.ManualDraftCaption;
        }

        if (_activeProfile is not null)
        {
            _draftProfileName = _activeProfile.Name;
            _draftProfileDescription = _activeProfile.Description;
        }

        if (syncOutputPath)
        {
            TryPopulateOutputPathIfEmpty();
        }

        RaiseComposerPropertyChanges();
        SchedulePreviewRefresh();
    }

    private EncodingProfile? BuildCurrentDraftProfile()
    {
        if (SelectedEncoder is null
            || SelectedRateControl is null
            || SelectedPreset is null
            || SelectedOutputFormat is null)
        {
            return null;
        }

        return new EncodingProfile(
            SelectedEncoder.Value,
            _draftProfileName,
            _draftProfileDescription,
            SelectedPreset.Value,
            SelectedTune?.Value ?? string.Empty,
            SelectedProfileOption?.Value ?? string.Empty,
            SelectedRateControl.Value,
            IsQualityControlVisible ? DraftQuality : 0,
            IsBitrateControlVisible ? (int?)Math.Round(DraftBitrate) : null,
            SelectedOutputFormat.Value,
            GetSanitizedAdditionalArguments(),
            GetSanitizedUhdParameters());
    }

    private EncoderCapability? GetSelectedCapability()
    {
        if (SelectedEncoder is null)
        {
            return null;
        }

        return Encoders.FirstOrDefault(item => item.Capability.Kind == SelectedEncoder.Value)?.Capability;
    }

    private static List<StringChoiceOption> BuildChoiceOptions(IEnumerable<string> values, string emptyLabel)
    {
        var result = new List<StringChoiceOption>
        {
            new(string.Empty, emptyLabel)
        };

        result.AddRange(values.Select(value => new StringChoiceOption(value, value)));
        return result;
    }

    private static StringChoiceOption? FindChoiceOption(
        IEnumerable<StringChoiceOption> options,
        string? preferredValue,
        bool fallbackToFirst)
    {
        if (!string.IsNullOrWhiteSpace(preferredValue))
        {
            var matched = options.FirstOrDefault(option => string.Equals(option.Value, preferredValue, StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched;
            }
        }

        if (string.IsNullOrWhiteSpace(preferredValue))
        {
            var empty = options.FirstOrDefault(static option => string.IsNullOrWhiteSpace(option.Value));
            if (empty is not null)
            {
                return empty;
            }
        }

        return fallbackToFirst ? options.FirstOrDefault() : null;
    }

    private string GetSanitizedAdditionalArguments()
    {
        if (SelectedEncoder is null)
        {
            return DraftAdditionalArguments.Trim();
        }

        var preserveRawSourceParameters = false;
        if (!string.IsNullOrWhiteSpace(SourcePath))
        {
            try
            {
                preserveRawSourceParameters = InputSourceSupport.ResolvePipelineKind(SourcePath) == InputPipelineKind.RawYuvFile;
            }
            catch (NotSupportedException)
            {
                preserveRawSourceParameters = false;
            }
        }

        return EncoderArgumentOverrideParser
            .Parse(SelectedEncoder.Value, DraftAdditionalArguments, preserveRawSourceParameters)
            .RemainingArguments
            .Trim();
    }

    private string GetSanitizedUhdParameters()
    {
        return SelectedEncoder?.Value == EncoderKind.X265
            ? DraftUhdParameters.Trim()
            : string.Empty;
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        PreferSystemEncoders = settings.PreferSystemEncoders;
        AutoCheckUpdatesOnStartup = settings.AutoCheckUpdatesOnStartup;
        MaxConcurrentEncodingJobs = settings.MaxConcurrentEncodingJobs;
        QueueCompletionAction = settings.QueueCompletionAction;
        WorkspaceRootPath = _appPaths.RootPath;
        _hasCompletedSetupGuide = settings.HasSeenSetupGuide;
        _manualToolPaths = new Dictionary<string, string>(settings.EffectiveManualToolPaths, StringComparer.OrdinalIgnoreCase);
        _hasRunInitialVsPluginDependencyUpdate = settings.HasRunInitialVsPluginDependencyUpdate;
        SelectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == settings.Theme) ?? ThemeOptions[0];
        SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Value == settings.Language) ?? LanguageOptions[0];
    }

    private void RunInitialVsPluginDependencyUpdateIfNeeded()
    {
        if (_hasRunInitialVsPluginDependencyUpdate)
        {
            return;
        }

        _hasRunInitialVsPluginDependencyUpdate = true;
        SaveSettings(updateStatusText: false);

        var readiness = _environmentReadinessReport;
        _ = Task.Run(async () =>
        {
            try
            {
                await _setupBootstrapService.RefreshVsPluginPackageDefinitionsAsync(readiness);
            }
            catch
            {
            }
        });
    }

    public async Task<string?> PrepareWorkspaceRootChangeAsync(string proposedWorkspaceRootPath)
    {
        if (HasRunningJobs
            || IsAutoCompressionRunning
            || IsAudioProcessingRunning
            || IsBluRayDemuxRunning
            || _isCheckingUpdates
            || _isDownloadingAppUpdateInstaller
            || _isSetupGuideInstallRunning
            || _isRefreshingSetupGuide
            || _isCheckingSetupDependencyUpdates)
        {
            return Texts.WorkspaceDirectoryChangeBlockedMessage;
        }

        string normalizedWorkspaceRootPath;
        try
        {
            normalizedWorkspaceRootPath = _appPaths.NormalizeWorkspaceRootPath(proposedWorkspaceRootPath);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        if (_appPaths.IsWorkspaceRootInsideInstallRoot(normalizedWorkspaceRootPath)
            || _appPaths.IsWorkspaceRootInsideProgramFiles(normalizedWorkspaceRootPath))
        {
            return Texts.WorkspaceDirectoryInvalidLocationMessage;
        }

        if (string.Equals(normalizedWorkspaceRootPath, WorkspaceRootPath, StringComparison.OrdinalIgnoreCase))
        {
            WorkspaceRootPath = normalizedWorkspaceRootPath;
            return null;
        }

        StatusText = Texts.WorkspaceDirectoryPreparingStatus;

        try
        {
            await Task.Run(() => _appPaths.PrepareWorkspaceRootChange(normalizedWorkspaceRootPath));
            WorkspaceRootPath = normalizedWorkspaceRootPath;
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private void SchedulePreviewRefresh()
    {
        if (_activeProfile is null)
        {
            CancelPendingPreviewRefresh();
            PreviewTitle = Texts.DraftNotReadyTitle;
            PreviewCommandLine = string.Empty;
            PreviewNotes = Texts.DraftNotReadyNotes;
            return;
        }

        CancelPendingPreviewRefresh();
        var requestVersion = Interlocked.Increment(ref _previewRefreshVersion);
        var cancellationTokenSource = new CancellationTokenSource();
        _previewRefreshCancellationTokenSource = cancellationTokenSource;

        _ = RefreshPreviewDeferredAsync(_activeProfile, requestVersion, cancellationTokenSource.Token);
    }

    private void ScheduleDraftInputRefresh()
    {
        CancelPendingDraftInputRefresh();
        var requestVersion = Interlocked.Increment(ref _draftInputRefreshVersion);
        var cancellationTokenSource = new CancellationTokenSource();
        _draftInputRefreshCancellationTokenSource = cancellationTokenSource;

        _ = RefreshDraftInputDeferredAsync(requestVersion, cancellationTokenSource.Token);
    }

    private async Task RefreshDraftInputDeferredAsync(int requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InputPathRefreshDelay, cancellationToken);
            if (!IsDraftInputRefreshCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            var hasPathState = !string.IsNullOrWhiteSpace(SourcePath) || !string.IsNullOrWhiteSpace(OutputPath);
            SetDraftInputRefreshPending(hasPathState);

            if (hasPathState && _activeProfile is not null)
            {
                PreviewTitle = Texts.DraftInputPreparingPreviewTitle;
                PreviewNotes = Texts.DraftInputPreparingPreviewNotes;
                await Task.Yield();
                if (!IsDraftInputRefreshCurrent(requestVersion, cancellationToken))
                {
                    return;
                }
            }

            _isApplyingDeferredDraftInputRefresh = true;
            try
            {
                TryPopulateOutputPathIfEmpty();
                RaiseDraftPathPropertyChanges();
            }
            finally
            {
                _isApplyingDeferredDraftInputRefresh = false;
            }

            if (!IsDraftInputRefreshCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            SetDraftInputRefreshPending(false);
            SchedulePreviewRefresh();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool IsDraftInputRefreshCurrent(int requestVersion, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && requestVersion == Volatile.Read(ref _draftInputRefreshVersion);
    }

    private void SetDraftInputRefreshPending(bool isPending)
    {
        if (_isDraftInputRefreshPending == isPending)
        {
            return;
        }

        _isDraftInputRefreshPending = isPending;
        OnPropertyChanged(nameof(DraftOutputPreviewText));
    }

    private void ScheduleAutoCompressionInputRefresh()
    {
        CancelPendingAutoCompressionInputRefresh();
        var requestVersion = Interlocked.Increment(ref _autoCompressionInputRefreshVersion);
        var cancellationTokenSource = new CancellationTokenSource();
        _autoCompressionInputRefreshCancellationTokenSource = cancellationTokenSource;

        _ = RefreshAutoCompressionInputDeferredAsync(requestVersion, cancellationTokenSource.Token);
    }

    private async Task RefreshAutoCompressionInputDeferredAsync(int requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InputPathRefreshDelay, cancellationToken);
            if (!IsAutoCompressionInputRefreshCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            var hasPathState = !string.IsNullOrWhiteSpace(AutoCompressionSourcePath) || !string.IsNullOrWhiteSpace(AutoCompressionOutputPath);
            SetAutoCompressionInputRefreshPending(hasPathState);

            if (hasPathState && !_isAutoCompressionRunning)
            {
                AutoCompressionStatusText = Texts.AutoCompressionInputPreparingStatus;
                await Task.Yield();
                if (!IsAutoCompressionInputRefreshCurrent(requestVersion, cancellationToken))
                {
                    return;
                }
            }

            _isApplyingDeferredAutoCompressionInputRefresh = true;
            try
            {
                TryPopulateAutoCompressionOutputPathIfEmpty();
                RaiseAutoCompressionInputPropertyChanges();
                RefreshAutoCompressionCommandPreview();
            }
            finally
            {
                _isApplyingDeferredAutoCompressionInputRefresh = false;
            }

            if (!IsAutoCompressionInputRefreshCurrent(requestVersion, cancellationToken))
            {
                return;
            }

            SetAutoCompressionInputRefreshPending(false);
            if (!_isAutoCompressionRunning && string.Equals(AutoCompressionStatusText, Texts.AutoCompressionInputPreparingStatus, StringComparison.Ordinal))
            {
                AutoCompressionStatusText = Texts.AutoCompressionIdleStatus;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool IsAutoCompressionInputRefreshCurrent(int requestVersion, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && requestVersion == Volatile.Read(ref _autoCompressionInputRefreshVersion);
    }

    private void SetAutoCompressionInputRefreshPending(bool isPending)
    {
        if (_isAutoCompressionInputRefreshPending == isPending)
        {
            return;
        }

        _isAutoCompressionInputRefreshPending = isPending;
        OnPropertyChanged(nameof(AutoCompressionOutputPreviewText));
    }

    private async Task RefreshPreviewDeferredAsync(
        EncodingProfile profile,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(120, cancellationToken);
            await UpdatePreviewAsync(profile, requestVersion, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (IsPreviewRequestCurrent(requestVersion, cancellationToken))
            {
                PreviewNotes = Texts.ActualDraftNotReadyMessage(ex.Message);
            }
        }
    }

    private void CancelPendingPreviewRefresh()
    {
        _previewRefreshCancellationTokenSource?.Cancel();
        _previewRefreshCancellationTokenSource?.Dispose();
        _previewRefreshCancellationTokenSource = null;
    }

    private void CancelPendingDraftInputRefresh()
    {
        _draftInputRefreshCancellationTokenSource?.Cancel();
        _draftInputRefreshCancellationTokenSource?.Dispose();
        _draftInputRefreshCancellationTokenSource = null;
    }

    private void CancelPendingAutoCompressionInputRefresh()
    {
        _autoCompressionInputRefreshCancellationTokenSource?.Cancel();
        _autoCompressionInputRefreshCancellationTokenSource?.Dispose();
        _autoCompressionInputRefreshCancellationTokenSource = null;
    }

    private bool IsPreviewRequestCurrent(int requestVersion, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested
            && requestVersion == Volatile.Read(ref _previewRefreshVersion);
    }

    private void TryPopulateOutputPathIfEmpty()
    {
        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            return;
        }

        var sourceDirectory = Path.GetDirectoryName(SourcePath);
        var suggestedPath = sourceDirectory ?? Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(OutputPath)
            && !string.Equals(OutputPath, _lastAutoOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetAutoOutputPath(suggestedPath);
    }

    private void SetAutoOutputPath(string path)
    {
        _isUpdatingOutputPath = true;

        try
        {
            OutputPath = path;
            _lastAutoOutputPath = path;
        }
        finally
        {
            _isUpdatingOutputPath = false;
        }
    }

    private void TryPopulateAutoCompressionOutputPathIfEmpty()
    {
        if (string.IsNullOrWhiteSpace(AutoCompressionSourcePath))
        {
            return;
        }

        var sourceDirectory = Path.GetDirectoryName(AutoCompressionSourcePath);
        var suggestedPath = sourceDirectory ?? Environment.CurrentDirectory;

        if (!string.IsNullOrWhiteSpace(AutoCompressionOutputPath)
            && !string.Equals(AutoCompressionOutputPath, _lastAutoCompressionOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetAutoCompressionOutputPath(suggestedPath);
    }

    private void SetAutoCompressionOutputPath(string path)
    {
        _isUpdatingAutoCompressionOutputPath = true;

        try
        {
            AutoCompressionOutputPath = path;
            _lastAutoCompressionOutputPath = path;
        }
        finally
        {
            _isUpdatingAutoCompressionOutputPath = false;
        }
    }

    private void ApplyAutoCompressionProgress(AutoCompressionProgress progress)
    {
        if (_activeAutoCompressionJobId != progress.JobId)
        {
            return;
        }

        SetAutoCompressionDisplayState(progress.State);

        if (progress.State == EncodingJobState.Completed)
        {
            ClampAutoCompressionProgressForTerminalState(EncodingJobState.Completed);
        }
        else if (progress.ProgressFraction.HasValue)
        {
            AutoCompressionProgressIsIndeterminate = false;
            AutoCompressionProgressPercent = progress.ProgressFraction.Value * 100;
        }
        else if (progress.State is EncodingJobState.Failed or EncodingJobState.Cancelled)
        {
            ClampAutoCompressionProgressForTerminalState(progress.State);
        }
        else if (_isAutoCompressionRunning)
        {
            AutoCompressionProgressIsIndeterminate = true;
        }

        if (!string.IsNullOrWhiteSpace(progress.Summary))
        {
            AutoCompressionStatusText = progress.Summary;
        }

        AppendAutoCompressionLogLine(progress.DetailLine);
    }

    private void AppendAutoCompressionLogLine(string line)
    {
        var normalized = ConsoleOutputLineNormalizer.Normalize(line);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (ToolLogLineClassifier.IsAutoCompressionTransientLine(normalized))
        {
            if (!string.Equals(_autoCompressionLiveLogLine, normalized, StringComparison.Ordinal))
            {
                _autoCompressionLiveLogLine = normalized;
                RefreshAutoCompressionLogText();
            }

            return;
        }

        _autoCompressionLiveLogLine = string.Empty;
        AppendAutoCompressionStageLogLine(normalized);
    }

    private void AppendAutoCompressionStageLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (_autoCompressionLogStageLines.Count > 0
            && string.Equals(_autoCompressionLogStageLines[^1], line, StringComparison.Ordinal))
        {
            RefreshAutoCompressionLogText();
            return;
        }

        _autoCompressionLogStageLines.Add(line);
        if (_autoCompressionLogStageLines.Count > AutoCompressionStageLogLimit)
        {
            _autoCompressionLogStageLines.RemoveAt(0);
        }

        RefreshAutoCompressionLogText();
    }

    private void RefreshAutoCompressionLogText()
    {
        var lines = new List<string>(_autoCompressionLogStageLines);
        if (!string.IsNullOrWhiteSpace(_autoCompressionLiveLogLine))
        {
            lines.Add(_autoCompressionLiveLogLine);
        }

        var joined = string.Join(Environment.NewLine, lines);
        if (joined.Length > AutoCompressionLogLimit)
        {
            joined = joined[^AutoCompressionLogLimit..];
        }

        AutoCompressionLog = joined;
    }

    private void ResetAutoCompressionLogState()
    {
        _autoCompressionLogBuilder.Clear();
        _autoCompressionLogStageLines.Clear();
        _autoCompressionLiveLogLine = string.Empty;
    }

    private void SetAutoCompressionRunningState(bool isRunning, Guid? activeJobId)
    {
        if (_isAutoCompressionRunning == isRunning && _activeAutoCompressionJobId == activeJobId)
        {
            return;
        }

        _isAutoCompressionRunning = isRunning;
        _activeAutoCompressionJobId = activeJobId;
        OnPropertyChanged(nameof(IsAutoCompressionRunning));
        OnPropertyChanged(nameof(CanStartAutoCompression));
        OnPropertyChanged(nameof(CanCancelAutoCompression));
        OnPropertyChanged(nameof(AutoCompressionProgressLabel));
        OnPropertyChanged(nameof(AutoCompressionProgressHintVisibility));

        if (isRunning)
        {
            CancelPendingQueueCompletionActionWait();
        }
        else
        {
            TryScheduleQueueCompletionActionAfterSuccessfulQueueDrain();
        }
    }

    private void SetAutoCompressionDisplayState(EncodingJobState? state)
    {
        if (_autoCompressionDisplayState == state)
        {
            return;
        }

        _autoCompressionDisplayState = state;
        OnPropertyChanged(nameof(AutoCompressionStatusPanelBorderBrush));
    }

    private void ClampAutoCompressionProgressForTerminalState(EncodingJobState state)
    {
        AutoCompressionProgressIsIndeterminate = false;
        AutoCompressionProgressPercent = state == EncodingJobState.Completed
            ? 100
            : Math.Min(AutoCompressionProgressPercent, 99.9);
    }

    private void DisposeAutoCompressionCancellation()
    {
        _autoCompressionCancellationTokenSource?.Dispose();
        _autoCompressionCancellationTokenSource = null;
    }

    private void RaiseJobStatePropertyChanges()
    {
        RaiseJobSummaryPropertyChanges();
    }

    private string BuildSelectedJobFramesText()
    {
        var currentFrame = SelectedJob?.CurrentFrame ?? 0;
        var totalFrames = SelectedJob?.TotalFrames?.ToString(CultureInfo.InvariantCulture) ?? "?";
        return $"{currentFrame.ToString(CultureInfo.InvariantCulture)}/{totalFrames} frames";
    }

    private int GetSelectedQueueJobStateCount(EncodingJobState state)
    {
        return _selectedQueueJobs.Count(job => job.State == state);
    }

    private static string FormatSelectedJobEta(TimeSpan? eta)
    {
        if (!eta.HasValue)
        {
            return "--:--:--";
        }

        var totalHours = Math.Max(0, (int)Math.Floor(eta.Value.TotalHours));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{eta.Value.Minutes:00}:{eta.Value.Seconds:00}");
    }

    private static string FormatSelectedJobSize(long? bytes)
    {
        if (!bytes.HasValue)
        {
            return "--";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes.Value;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{size:0} {units[unitIndex]}"
            : $"{size:0.#} {units[unitIndex]}";
    }

    private void RaiseSelectedJobProgressMetricPropertyChanges()
    {
        OnPropertyChanged(nameof(SelectedJobProgressValue));
        OnPropertyChanged(nameof(SelectedJobProgressPrimaryText));
        OnPropertyChanged(nameof(SelectedJobProgressSecondaryText));
        OnPropertyChanged(nameof(SelectedJobProgressPercentText));
        OnPropertyChanged(nameof(SelectedJobSourcePreparationVisibility));
        OnPropertyChanged(nameof(SelectedJobSourcePreparationText));
        OnPropertyChanged(nameof(SelectedJobFramesText));
        OnPropertyChanged(nameof(SelectedJobFpsText));
        OnPropertyChanged(nameof(SelectedJobBitrateText));
        OnPropertyChanged(nameof(SelectedJobEtaText));
        OnPropertyChanged(nameof(SelectedJobEstimatedSizeText));
    }

    private void SelectedJob_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, SelectedJob) || _isDisposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName))
        {
            RaiseSelectedJobPropertyChanges();
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EncodingJobItemViewModel.Summary):
            case nameof(EncodingJobItemViewModel.StateLabel):
                OnPropertyChanged(nameof(SelectedJobSummary));
                break;

            case nameof(EncodingJobItemViewModel.DisplayCommand):
                OnPropertyChanged(nameof(SelectedJobCommandText));
                break;

            case nameof(EncodingJobItemViewModel.ProgressValue):
            case nameof(EncodingJobItemViewModel.ProgressPercentLabel):
            case nameof(EncodingJobItemViewModel.IsSourcePreparation):
            case nameof(EncodingJobItemViewModel.SourcePreparationText):
            case nameof(EncodingJobItemViewModel.HasSourcePreparationText):
            case nameof(EncodingJobItemViewModel.CurrentFrame):
            case nameof(EncodingJobItemViewModel.TotalFrames):
            case nameof(EncodingJobItemViewModel.FramesPerSecond):
            case nameof(EncodingJobItemViewModel.BitrateKbps):
            case nameof(EncodingJobItemViewModel.Eta):
            case nameof(EncodingJobItemViewModel.ProgressTelemetryPrimaryLine):
            case nameof(EncodingJobItemViewModel.EstimatedFileSizeBytes):
            case nameof(EncodingJobItemViewModel.ProgressTelemetrySecondaryLine):
                RaiseSelectedJobProgressMetricPropertyChanges();
                break;

            case nameof(EncodingJobItemViewModel.Log):
                OnPropertyChanged(nameof(SelectedJobLogText));
                break;
        }
    }

    private void RaiseSummaryPropertyChanges()
    {
        OnPropertyChanged(nameof(SelectedJobSummary));
    }

    private void RaiseComposerPropertyChanges()
    {
        OnPropertyChanged(nameof(HasUnsavedTemplateChanges));
        OnPropertyChanged(nameof(SuggestedOutputExtension));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        OnPropertyChanged(nameof(DraftOutputPreviewText));
        OnPropertyChanged(nameof(QualityInputLabel));
        OnPropertyChanged(nameof(BitrateInputLabel));
        OnPropertyChanged(nameof(IsQualityControlVisible));
        OnPropertyChanged(nameof(IsBitrateControlVisible));
        OnPropertyChanged(nameof(IsX265Selected));
        OnPropertyChanged(nameof(X265UhdVisibility));
        OnPropertyChanged(nameof(DraftConstraintWarningText));
        OnPropertyChanged(nameof(DraftConstraintWarningVisibility));
        OnPropertyChanged(nameof(DraftQualityVisibility));
        OnPropertyChanged(nameof(DraftBitrateVisibility));
        OnPropertyChanged(nameof(CanQueueJob));
    }

    private void RaiseTemplateLockPropertyChanges()
    {
        OnPropertyChanged(nameof(IsEditingPinnedTemplate));
        OnPropertyChanged(nameof(CanEditTemplateDraft));
    }

    private void RaiseDraftPathPropertyChanges()
    {
        OnPropertyChanged(nameof(CanQueueJob));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        OnPropertyChanged(nameof(DraftOutputPreviewText));
    }

    private void RaiseAutoCompressionInputPropertyChanges()
    {
        OnPropertyChanged(nameof(CanStartAutoCompression));
        OnPropertyChanged(nameof(AutoCompressionSuggestedOutputFileName));
        OnPropertyChanged(nameof(AutoCompressionOutputPreviewText));
    }

    private void RaiseJobSummaryPropertyChanges()
    {
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(EmptyQueueVisibility));
        OnPropertyChanged(nameof(QueueSummary));
        RaiseQueueSelectionPropertyChanges();
    }

    private void RaiseSelectedJobPropertyChanges()
    {
        OnPropertyChanged(nameof(SelectedJobSummary));
        RaiseSelectedJobProgressMetricPropertyChanges();
        OnPropertyChanged(nameof(SelectedJobCommandText));
        OnPropertyChanged(nameof(SelectedJobLogText));
    }

    private void RaiseQueueSelectionPropertyChanges()
    {
        OnPropertyChanged(nameof(SelectedQueueJobCount));
        OnPropertyChanged(nameof(SelectedQueuedJobCount));
        OnPropertyChanged(nameof(SelectedRunningJobCount));
        OnPropertyChanged(nameof(SelectedCancelableQueueJobCount));
        OnPropertyChanged(nameof(SelectedRemovableQueueJobCount));
        OnPropertyChanged(nameof(QueueSelectionStatusText));
        OnPropertyChanged(nameof(CanSelectAllQueueJobs));
        OnPropertyChanged(nameof(CanInvertQueueSelection));
        OnPropertyChanged(nameof(CanClearQueueSelection));
        OnPropertyChanged(nameof(CanStartSelectedJobs));
        OnPropertyChanged(nameof(CanCancelSelectedJobs));
        OnPropertyChanged(nameof(CanDeleteSelectedJobs));
        RaiseSelectedJobPropertyChanges();
    }

    private IEnumerable<EncodingJobItemViewModel> NormalizeSelectedQueueJobs(IEnumerable<EncodingJobItemViewModel> selectedJobs)
    {
        var selectedJobIds = selectedJobs
            .Where(static job => job is not null)
            .Select(static job => job.JobId)
            .ToHashSet();

        foreach (var job in Jobs)
        {
            if (!selectedJobIds.Contains(job.JobId))
            {
                continue;
            }

            yield return job;
        }
    }

    private void PruneSelectedQueueJobs()
    {
        var normalizedSelection = NormalizeSelectedQueueJobs(_selectedQueueJobs).ToList();
        if (_selectedQueueJobs.Count == normalizedSelection.Count
            && _selectedQueueJobs.SequenceEqual(normalizedSelection))
        {
            RaiseQueueSelectionPropertyChanges();
            return;
        }

        _selectedQueueJobs.Clear();
        _selectedQueueJobs.AddRange(normalizedSelection);
        RaiseQueueSelectionPropertyChanges();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private IEnumerable<ThemeOption> BuildThemeOptions()
    {
        return
        [
            new ThemeOption(AppThemePreference.Default, Texts.ThemeLabel(AppThemePreference.Default)),
            new ThemeOption(AppThemePreference.Light, Texts.ThemeLabel(AppThemePreference.Light)),
            new ThemeOption(AppThemePreference.Dark, Texts.ThemeLabel(AppThemePreference.Dark))
        ];
    }

    private static IEnumerable<StringChoiceOption> BuildConcurrentEncodingJobOptions()
    {
        return Enumerable
            .Range(MinConcurrentEncodingJobs, MaxConcurrentEncodingJobsLimit)
            .Select(value =>
            {
                var text = value.ToString(CultureInfo.InvariantCulture);
                return new StringChoiceOption(text, text);
            });
    }

    private IEnumerable<StringChoiceOption> BuildQueueCompletionActionOptions()
    {
        return
        [
            new StringChoiceOption(QueueCompletionAction.None.ToString(), Texts.QueueCompletionActionLabel(QueueCompletionAction.None)),
            new StringChoiceOption(QueueCompletionAction.Sleep.ToString(), Texts.QueueCompletionActionLabel(QueueCompletionAction.Sleep)),
            new StringChoiceOption(QueueCompletionAction.Shutdown.ToString(), Texts.QueueCompletionActionLabel(QueueCompletionAction.Shutdown))
        ];
    }

    private void ApplyLanguage(AppLanguage language)
    {
        Texts = new AppText(language);

        var themePreference = CurrentThemePreference;
        ReplaceItems(ThemeOptions, BuildThemeOptions());
        _selectedTheme = ThemeOptions.FirstOrDefault(option => option.Value == themePreference) ?? ThemeOptions[0];
        OnPropertyChanged(nameof(SelectedTheme));
        ReplaceItems(QueueCompletionActionOptions, BuildQueueCompletionActionOptions());
        SyncSelectedQueueCompletionActionOption(QueueCompletionAction);

        foreach (var job in Jobs)
        {
            job.SetLanguage(language);
        }

        RefreshTemplateLibraryItems();
        RaiseSetupGuidePropertyChanges();
        RefreshSelectedProfileCaption();
        RaiseSummaryPropertyChanges();
        RaiseComposerPropertyChanges();
        RaiseJobSummaryPropertyChanges();
        RaiseSelectedJobPropertyChanges();

        if (_activeProfile is null)
        {
            PreviewTitle = Texts.DraftNotReadyTitle;
            PreviewNotes = Texts.DraftNotReadyNotes;
        }

        if (string.IsNullOrWhiteSpace(OutputPath) && string.IsNullOrWhiteSpace(SourcePath))
        {
            OnPropertyChanged(nameof(SuggestedOutputFileName));
        }

        OnPropertyChanged(nameof(DraftOutputPreviewText));
        RaiseAppUpdatePropertyChanges();

        if (!_isAutoCompressionRunning)
        {
            SetAutoCompressionDisplayState(null);
            AutoCompressionStatusText = Texts.AutoCompressionIdleStatus;
        }

        OnPropertyChanged(nameof(AutoCompressionSuggestedOutputFileName));
        OnPropertyChanged(nameof(AutoCompressionOutputPreviewText));
        OnPropertyChanged(nameof(CanStartAutoCompression));
        OnPropertyChanged(nameof(CanCancelAutoCompression));
        OnPropertyChanged(nameof(AutoCompressionProgressLabel));
        OnPropertyChanged(nameof(AutoCompressionProgressHint));
        OnPropertyChanged(nameof(AutoCompressionProgressHintVisibility));
        ApplyAudioProcessingLanguageState();
        ApplyBluRayDemuxLanguageState();
        RaiseSetupGuidePropertyChanges();

        SchedulePreviewRefresh();
    }

    private string GetAppUpdateStatusText()
    {
        var currentVersion = GetKnownCurrentAppVersion();
        if (!string.IsNullOrWhiteSpace(_lastAppUpdateErrorMessage))
        {
            return _lastAppUpdateErrorMessage;
        }

        if (_isDownloadingAppUpdateInstaller && _lastAppUpdateResult is not null)
        {
            return Texts.AppUpdateDownloadingStatus(_lastAppUpdateResult.LatestVersion, _appUpdateDownloadProgressPercent);
        }

        if (_lastAppUpdateResult is null)
        {
            return Texts.AppUpdateIdleStatus;
        }

        return !_lastAppUpdateResult.HasPublishedRelease
            ? Texts.AppReleaseNotPublishedStatus(currentVersion)
            : _lastAppUpdateResult.UpdateAvailable
                ? _lastAppUpdateResult.CanDownloadInstaller
                    ? Texts.AppUpdateAvailableStatus(currentVersion, _lastAppUpdateResult.LatestVersion)
                    : Texts.AppUpdateManualDownloadStatus(currentVersion, _lastAppUpdateResult.LatestVersion)
                : _lastAppUpdateResult.IsCurrentVersionNewerThanRelease
                    ? Texts.AppCurrentVersionAheadStatus(currentVersion, _lastAppUpdateResult.LatestVersion)
                    : _lastAppUpdateResult.VersionsComparable
                        ? Texts.AppAlreadyLatestStatus(currentVersion)
                        : Texts.AppUpdateComparisonUnavailableStatus(currentVersion, _lastAppUpdateResult.LatestVersion);
    }

    private string GetKnownCurrentAppVersion()
    {
        return _lastAppUpdateResult?.CurrentVersion ?? GetCurrentAppVersionLabel();
    }

    private void RaiseAppUpdatePropertyChanges()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsCheckingAppUpdates));
        OnPropertyChanged(nameof(IsDownloadingAppUpdateInstaller));
        OnPropertyChanged(nameof(IsAppUpdateActionInProgress));
        OnPropertyChanged(nameof(IsAppUpdateAvailable));
        OnPropertyChanged(nameof(CanDownloadAppUpdateInstaller));
        OnPropertyChanged(nameof(HasAppUpdateError));
        OnPropertyChanged(nameof(AppUpdateActionText));
        OnPropertyChanged(nameof(AppUpdateActionIcon));
        OnPropertyChanged(nameof(CanExecuteAppUpdateAction));
        OnPropertyChanged(nameof(AppUpdateProgressVisibility));
        OnPropertyChanged(nameof(AppUpdateReleaseUrl));
        OnPropertyChanged(nameof(AppCurrentVersionText));
        OnPropertyChanged(nameof(AppLatestVersionText));
        OnPropertyChanged(nameof(AppLatestVersionVisibility));
        OnPropertyChanged(nameof(AppUpdateStatusText));
    }

    private static string GetCurrentAppVersionLabel()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return NormalizeVersionLabel(informationalVersion)
            ?? NormalizeVersionLabel(assembly.GetName().Version?.ToString())
            ?? "0.0.0";
    }

    private static string? NormalizeVersionLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 1
            && (trimmed[0] == 'v' || trimmed[0] == 'V')
            && char.IsDigit(trimmed[1]))
        {
            trimmed = trimmed[1..];
        }

        var versionMatch = Regex.Match(trimmed, "(?<base>\\d+\\.\\d+(?:\\.\\d+)*)(?<suffix>[0-9a-f]{7,12})?", RegexOptions.IgnoreCase);
        if (versionMatch.Success)
        {
            var suffix = versionMatch.Groups["suffix"].Success
                ? versionMatch.Groups["suffix"].Value.ToLowerInvariant()
                : string.Empty;
            return versionMatch.Groups["base"].Value + suffix;
        }

        return trimmed;
    }

    partial void InitializeAudioProcessingState();

    partial void DisposeAudioProcessingState();

    partial void HandleAudioEnvironmentReadinessApplied();

    partial void ApplyAudioProcessingLanguageState();

    partial void InitializeBluRayDemuxState();

    partial void DisposeBluRayDemuxState();

    partial void HandleBluRayEnvironmentReadinessApplied();

    partial void ApplyBluRayDemuxLanguageState();

    private void RefreshSelectedProfileCaption()
    {
        if (_currentTemplateSelectionKey is not null && HasUnsavedTemplateChanges)
        {
            SelectedProfileCaption = Texts.ManualDraftCaption;
            return;
        }

        if (_currentTemplateSelectionKey is not null)
        {
            if (_currentTemplateSelectionKey.StartsWith("user:", StringComparison.Ordinal))
            {
                var name = string.IsNullOrWhiteSpace(DraftTemplateName) ? _activeProfile?.Name ?? string.Empty : DraftTemplateName;
                SelectedProfileCaption = Texts.UserCaption(name);
                return;
            }
        }

        if (_editingTemplateId is not null)
        {
            var name = string.IsNullOrWhiteSpace(DraftTemplateName) ? _activeProfile?.Name ?? string.Empty : DraftTemplateName;
            SelectedProfileCaption = Texts.UserCaption(name);
            return;
        }

        if (string.IsNullOrWhiteSpace(DraftTemplateName)
            && string.IsNullOrWhiteSpace(DraftTemplateNotes)
            && _currentTemplateSelectionKey is null)
        {
            SelectedProfileCaption = Texts.NewTemplateCaption;
            return;
        }

        if (_activeProfile is null)
        {
            SelectedProfileCaption = Texts.NoProfileSelectedCaption;
            return;
        }

        SelectedProfileCaption = Texts.ManualDraftCaption;
    }

    private string BuildBinarySourceSummary(DiscoveredEncoderBinary binary)
    {
        return Texts.BinarySourceSummary(binary.Source, binary.SourceLabel);
    }

    private async Task RefreshSystemBinariesAsync(CancellationToken cancellationToken = default)
    {
        var discoveredBinaries = await Task.Run(
            () => _encoderDiscoveryService.DiscoverSystemBinaries(),
            cancellationToken);

        ReplaceItems(DetectedSystemBinaries, discoveredBinaries);
    }

    private Task<string> BuildDisplayCommandAsync(
        EncodingJobRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => _jobRunner.BuildDisplayCommand(request), cancellationToken);
    }

    private async Task ResolveJobDisplayCommandAsync(EncodingJobItemViewModel job, EncodingJobRequest request)
    {
        try
        {
            var displayCommand = await BuildDisplayCommandAsync(request);
            job.UpdateDisplayCommand(displayCommand);
            if (ReferenceEquals(SelectedJob, job))
            {
                OnPropertyChanged(nameof(SelectedJobCommandText));
            }
        }
        catch
        {
        }
    }

    private DiscoveredEncoderBinary? ResolveEncoderFromCachedSources(
        EncoderKind kind,
        EncoderArchitecture preferredArchitecture,
        EncoderCatalogItem? catalogItem = null)
    {
        catalogItem ??= Encoders.FirstOrDefault(item => item.Capability.Kind == kind);

        var localBinaries = catalogItem?.Binaries ?? [];
        var localCandidate = localBinaries
            .Where(static binary => binary.Exists)
            .OrderByDescending(binary => binary.Architecture == preferredArchitecture)
            .Select(binary => new DiscoveredEncoderBinary(
                kind,
                binary.Architecture,
                binary.LocalPath,
                EncoderBinarySource.LocalToolset,
                "encoders",
                binary.DetectedVersion))
            .FirstOrDefault();

        if (localCandidate is not null)
        {
            return localCandidate;
        }

        if (!PreferSystemEncoders)
        {
            return null;
        }

        return DetectedSystemBinaries
            .Where(binary => binary.Kind == kind)
            .OrderByDescending(binary => binary.Architecture == preferredArchitecture)
            .ThenBy(binary => binary.Source)
            .FirstOrDefault();
    }
}

public enum MoveQueuedJobMode
{
    Next,
    Top,
    Up,
    Down,
    Bottom
}

public sealed record QueueJobPreflightResult(
    string BaseOutputPath,
    string FinalOutputPath,
    EncodingJobItemViewModel? DuplicateJob,
    EncodingJobItemViewModel? RunningOutputConflict,
    int QueuedOutputConflictCount,
    bool DiskOutputPathExists,
    string? ValidationError)
{
    public bool IsOutputPathAutoRenamed =>
        !string.IsNullOrWhiteSpace(BaseOutputPath)
        && !string.IsNullOrWhiteSpace(FinalOutputPath)
        && !string.Equals(BaseOutputPath, FinalOutputPath, StringComparison.OrdinalIgnoreCase);
}
