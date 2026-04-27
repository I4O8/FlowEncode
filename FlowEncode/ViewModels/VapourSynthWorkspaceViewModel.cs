using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowEncode.Application;
using FlowEncode.Domain;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace FlowEncode.ViewModels;

public sealed class VapourSynthWorkspaceViewModel : ObservableObject, IDisposable
{
    private const int MaxPreviewLogLines = 500;
    private readonly AppLaunchActivation _launchActivation;
    private readonly IVapourSynthWorkspaceService _workspaceService;
    private readonly ObservableCollection<VapourSynthRecentFileItem> _recentFiles = [];
    private readonly Queue<string> _previewLogLines = [];
    private CancellationTokenSource? _sessionSaveCancellationTokenSource;
    private AppText _texts;
    private string? _currentFilePath;
    private string _currentContent;
    private string _savedContent;
    private string _preferredLineEnding;
    private string _logText;
    private bool _isDirty;
    private bool _forceDirtyUntilSave;
    private bool _isInitialized;
    private string _workspaceStatusText;
    private Func<AppText, string>? _workspaceStatusFormatter;
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private int _lineCount = 1;
    private int _charCount;

    public VapourSynthWorkspaceViewModel(
        IVapourSynthWorkspaceService workspaceService,
        IAppSettingsService settingsService,
        AppLaunchActivation launchActivation)
    {
        _launchActivation = launchActivation;
        _workspaceService = workspaceService;
        _texts = new AppText(settingsService.Load().Language);
        _currentContent = string.Empty;
        _savedContent = string.Empty;
        _preferredLineEnding = Environment.NewLine;
        _workspaceStatusFormatter = static texts => texts.VapourSynthEditorLoadingStatus;
        _workspaceStatusText = _workspaceStatusFormatter(_texts);
        _logText = _texts.VapourSynthLogEmptyPlaceholder;
        RecentFiles = new ReadOnlyObservableCollection<VapourSynthRecentFileItem>(_recentFiles);
    }

    public AppText Texts
    {
        get => _texts;
        private set => SetProperty(ref _texts, value);
    }

    public ReadOnlyObservableCollection<VapourSynthRecentFileItem> RecentFiles { get; }

    public string EditorAssetsRootPath => _workspaceService.EditorAssetsRootPath;

    public string CurrentContent => _currentContent;

    public string? CurrentFilePath => _currentFilePath;

    public bool IsInitialized => _isInitialized;

    public bool HasUnsavedChanges => _isDirty;

    public bool HasRecentFiles => _recentFiles.Count > 0;

    public Visibility RecentFilesVisibility => HasRecentFiles ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RecentFilesEmptyVisibility => HasRecentFiles ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DirtyBadgeVisibility => _isDirty ? Visibility.Visible : Visibility.Collapsed;

    public bool CanReload => !string.IsNullOrWhiteSpace(_currentFilePath);

    public string DocumentTitle
    {
        get
        {
            var fileName = string.IsNullOrWhiteSpace(_currentFilePath)
                ? Texts.VapourSynthUntitledDocument
                : Path.GetFileName(_currentFilePath);
            return _isDirty ? $"{fileName} *" : fileName;
        }
    }

    public string DocumentPathText => string.IsNullOrWhiteSpace(_currentFilePath)
        ? Texts.VapourSynthPathPlaceholder
        : _currentFilePath;

    public string WorkspaceStatusText
    {
        get => _workspaceStatusText;
        private set => SetProperty(ref _workspaceStatusText, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public string EditorStatusText => Texts.VapourSynthEditorCursorStatus(
        _caretLine,
        _caretColumn,
        _lineCount,
        _charCount,
        _isDirty);

    public void ApplyLanguage(AppLanguage language)
    {
        if (Texts.Language == language)
        {
            return;
        }

        Texts = new AppText(language);

        if (_workspaceStatusFormatter is not null)
        {
            WorkspaceStatusText = _workspaceStatusFormatter(Texts);
        }

        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(DocumentPathText));
        OnPropertyChanged(nameof(EditorStatusText));
        UpdateLogText();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        var starterDocument = await _workspaceService.CreateNewDocumentAsync();
        var session = await _workspaceService.LoadSessionAsync();
        var normalizedRecentFiles = NormalizeExistingRecentFiles(session?.RecentFiles);
        ReplaceRecentFiles(normalizedRecentFiles);

        var launchFilePath = _launchActivation.RequestedVapourSynthFilePath;
        if (!string.IsNullOrWhiteSpace(launchFilePath))
        {
            await InitializeFromLaunchRequestAsync(launchFilePath, starterDocument.Content);
            return;
        }

        var shouldPersistSession = session is not null
            && normalizedRecentFiles.Count != session.RecentFiles.Count;

        if (session?.IsDirty == true && !string.IsNullOrWhiteSpace(session.ActiveContent))
        {
            shouldPersistSession |= await RestoreDirtySessionAsync(session, starterDocument.Content);

            if (shouldPersistSession)
            {
                await FlushSessionAsync();
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(session?.ActiveFilePath))
        {
            if (File.Exists(session.ActiveFilePath))
            {
                var restoredDocument = await _workspaceService.OpenDocumentAsync(session.ActiveFilePath);
                ApplyDocumentState(restoredDocument.FilePath, restoredDocument.Content, restoredDocument.Content, false);
                SetWorkspaceStatus(texts => HasExternalFileChanges(session, restoredDocument.Content)
                    ? texts.VapourSynthRestoredUpdatedDocumentStatus(restoredDocument.FilePath!)
                    : texts.VapourSynthRestoredDocumentStatus(restoredDocument.FilePath!));
            }
            else
            {
                ApplyDocumentState(starterDocument.FilePath, starterDocument.Content, starterDocument.Content, false);
                SetWorkspaceStatus(texts => texts.VapourSynthRestoredMissingFileStatus(session.ActiveFilePath));
                shouldPersistSession = true;
            }

            if (shouldPersistSession)
            {
                await FlushSessionAsync();
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(session?.ActiveContent))
        {
            ApplyDocumentState(null, session.ActiveContent, session.ActiveContent, false);
            SetWorkspaceStatus(static texts => texts.VapourSynthEditorReadyStatus);

            if (shouldPersistSession)
            {
                await FlushSessionAsync();
            }

            return;
        }

        ApplyDocumentState(starterDocument.FilePath, starterDocument.Content, starterDocument.Content, false);
        SetWorkspaceStatus(static texts => texts.VapourSynthEditorReadyStatus);
        await FlushSessionAsync();
    }

    public void ApplyEditorBuffer(string content, int line, int column, int lineCount, int charCount)
    {
        _currentContent = NormalizeLineEndings(content);
        _caretLine = Math.Max(1, line);
        _caretColumn = Math.Max(1, column);
        _lineCount = Math.Max(1, lineCount);
        _charCount = Math.Max(0, charCount);

        var previousDirty = _isDirty;
        _isDirty = _forceDirtyUntilSave
            || !string.Equals(_currentContent, _savedContent, StringComparison.Ordinal);

        if (previousDirty != _isDirty)
        {
            OnPropertyChanged(nameof(DocumentTitle));
            OnPropertyChanged(nameof(DirtyBadgeVisibility));
        }

        OnPropertyChanged(nameof(EditorStatusText));
        ScheduleSessionSave();
    }

    public void ApplyCursorState(int line, int column, int lineCount, int charCount)
    {
        _caretLine = Math.Max(1, line);
        _caretColumn = Math.Max(1, column);
        _lineCount = Math.Max(1, lineCount);
        _charCount = Math.Max(0, charCount);
        OnPropertyChanged(nameof(EditorStatusText));
    }

    public async Task CreateNewDocumentAsync()
    {
        var document = await _workspaceService.CreateNewDocumentAsync();
        ApplyDocumentState(document.FilePath, document.Content, document.Content, false);
        SetWorkspaceStatus(static texts => texts.VapourSynthNewDocumentStatus);
        await FlushSessionAsync();
    }

    public async Task OpenDocumentAsync(string filePath)
    {
        var document = await _workspaceService.OpenDocumentAsync(filePath);
        ApplyDocumentState(document.FilePath, document.Content, document.Content, false);
        AddRecentFile(filePath);
        SetWorkspaceStatus(texts => texts.VapourSynthOpenedStatus(filePath));
        await FlushSessionAsync();
    }

    public async Task ReloadDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            return;
        }

        var document = await _workspaceService.OpenDocumentAsync(_currentFilePath);
        ApplyDocumentState(document.FilePath, document.Content, document.Content, false);
        SetWorkspaceStatus(texts => texts.VapourSynthReloadedStatus(_currentFilePath));
        await FlushSessionAsync();
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            throw new InvalidOperationException("Current document has no file path.");
        }

        await SaveAsAsync(_currentFilePath);
    }

    public async Task SaveAsAsync(string filePath)
    {
        var document = await _workspaceService.SaveDocumentAsync(filePath, RestorePreferredLineEndings(_currentContent));
        ApplyDocumentState(document.FilePath, document.Content, document.Content, false);
        AddRecentFile(filePath);
        SetWorkspaceStatus(texts => texts.VapourSynthSavedStatus(filePath));
        await FlushSessionAsync();
    }

    public async Task FlushSessionAsync(bool discardUnsavedChanges = false)
    {
        CancelScheduledSessionSave();
        await _workspaceService.SaveSessionAsync(BuildSession(discardUnsavedChanges));
    }

    public void RemoveRecentFile(string filePath)
    {
        var matches = _recentFiles
            .Where(item => string.Equals(item.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var item in matches)
        {
            _recentFiles.Remove(item);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(RecentFilesVisibility));
        OnPropertyChanged(nameof(RecentFilesEmptyVisibility));
        ScheduleSessionSave();
    }

    public void SetWorkspaceStatus(string statusText)
    {
        _workspaceStatusFormatter = null;
        WorkspaceStatusText = statusText;
    }

    public void SetWorkspaceStatus(Func<AppText, string> statusFormatter)
    {
        _workspaceStatusFormatter = statusFormatter;
        WorkspaceStatusText = statusFormatter(Texts);
    }

    public void AppendPreviewLog(VapourSynthPreviewLogEntry entry)
    {
        var timestamp = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
        var level = entry.Level switch
        {
            VapourSynthPreviewLogLevel.Warning => "WARN",
            VapourSynthPreviewLogLevel.Error => "ERROR",
            _ => "INFO"
        };
        var source = string.IsNullOrWhiteSpace(entry.Source)
            ? "preview"
            : entry.Source.Trim();
        var message = NormalizeLineEndings(entry.Message);

        foreach (var line in message.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            _previewLogLines.Enqueue($"[{timestamp}] [{level}] [{source}] {line}");
        }

        while (_previewLogLines.Count > MaxPreviewLogLines)
        {
            _previewLogLines.Dequeue();
        }

        UpdateLogText();
    }

    public void ClearPreviewLog()
    {
        _previewLogLines.Clear();
        UpdateLogText();
    }

    public void Dispose()
    {
        CancelScheduledSessionSave();
    }

    private void AddRecentFile(string filePath)
    {
        RemoveRecentFile(filePath);
        _recentFiles.Insert(0, new VapourSynthRecentFileItem(filePath));

        while (_recentFiles.Count > 10)
        {
            _recentFiles.RemoveAt(_recentFiles.Count - 1);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(RecentFilesVisibility));
        OnPropertyChanged(nameof(RecentFilesEmptyVisibility));
    }

    private void ReplaceRecentFiles(System.Collections.Generic.IEnumerable<string>? recentFiles)
    {
        _recentFiles.Clear();

        foreach (var filePath in (recentFiles ?? []).Where(static path => !string.IsNullOrWhiteSpace(path)))
        {
            _recentFiles.Add(new VapourSynthRecentFileItem(filePath));
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(RecentFilesVisibility));
        OnPropertyChanged(nameof(RecentFilesEmptyVisibility));
    }

    private void ApplyDocumentState(string? filePath, string content, string savedContent, bool isDirty, bool forceDirtyUntilSave = false)
    {
        var normalizedContent = NormalizeLineEndings(content);
        var normalizedSavedContent = NormalizeLineEndings(savedContent);

        _currentFilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
        _currentContent = normalizedContent;
        _savedContent = normalizedSavedContent;
        _preferredLineEnding = DetectLineEnding(string.IsNullOrEmpty(savedContent) ? content : savedContent);
        _forceDirtyUntilSave = forceDirtyUntilSave;
        _isDirty = isDirty || _forceDirtyUntilSave;

        _lineCount = CountLines(_currentContent);
        _charCount = _currentContent.Length;
        _caretLine = 1;
        _caretColumn = 1;

        OnPropertyChanged(nameof(CurrentContent));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(DocumentTitle));
        OnPropertyChanged(nameof(DocumentPathText));
        OnPropertyChanged(nameof(EditorStatusText));
        OnPropertyChanged(nameof(DirtyBadgeVisibility));
        OnPropertyChanged(nameof(CanReload));
    }

    private VapourSynthWorkspaceSession BuildSession(bool discardUnsavedChanges)
    {
        var recentFiles = _recentFiles.Select(item => item.FullPath).ToArray();
        var savedContentHash = BuildSavedContentHash();

        if (discardUnsavedChanges)
        {
            return !string.IsNullOrWhiteSpace(_currentFilePath)
                ? new VapourSynthWorkspaceSession(_currentFilePath, null, false, recentFiles, savedContentHash)
                : new VapourSynthWorkspaceSession(null, null, false, recentFiles, null);
        }

        if (_isDirty)
        {
            return new VapourSynthWorkspaceSession(_currentFilePath, _currentContent, true, recentFiles, savedContentHash);
        }

        return !string.IsNullOrWhiteSpace(_currentFilePath)
            ? new VapourSynthWorkspaceSession(_currentFilePath, null, false, recentFiles, savedContentHash)
            : new VapourSynthWorkspaceSession(null, _currentContent, false, recentFiles, null);
    }

    private void ScheduleSessionSave()
    {
        if (!_isInitialized)
        {
            return;
        }

        CancelScheduledSessionSave();
        _sessionSaveCancellationTokenSource = new CancellationTokenSource();
        _ = PersistSessionAfterDelayAsync(_sessionSaveCancellationTokenSource.Token);
    }

    private async Task PersistSessionAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken);
            await _workspaceService.SaveSessionAsync(BuildSession(false), cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private void CancelScheduledSessionSave()
    {
        if (_sessionSaveCancellationTokenSource is null)
        {
            return;
        }

        _sessionSaveCancellationTokenSource.Cancel();
        _sessionSaveCancellationTokenSource.Dispose();
        _sessionSaveCancellationTokenSource = null;
    }

    private async Task<string> LoadSavedContentOrFallbackAsync(string? filePath, string fallbackContent)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            var document = await _workspaceService.OpenDocumentAsync(filePath);
            return document.Content;
        }

        return fallbackContent;
    }

    private async Task InitializeFromLaunchRequestAsync(string launchFilePath, string starterContent)
    {
        if (!File.Exists(launchFilePath))
        {
            ApplyDocumentState(null, starterContent, starterContent, false);
            SetWorkspaceStatus(texts => texts.VapourSynthLaunchFileMissingStatus(launchFilePath));
            await FlushSessionAsync();
            return;
        }

        try
        {
            var document = await _workspaceService.OpenDocumentAsync(launchFilePath);
            ApplyDocumentState(document.FilePath, document.Content, document.Content, false);
            AddRecentFile(launchFilePath);
            SetWorkspaceStatus(texts => texts.VapourSynthOpenedStatus(launchFilePath));
        }
        catch (Exception ex)
        {
            ApplyDocumentState(null, starterContent, starterContent, false);
            SetWorkspaceStatus(texts => texts.VapourSynthLaunchOpenFailedStatus(launchFilePath, ex.Message));
        }

        await FlushSessionAsync();
    }

    private async Task<bool> RestoreDirtySessionAsync(VapourSynthWorkspaceSession session, string starterContent)
    {
        if (string.IsNullOrWhiteSpace(session.ActiveFilePath))
        {
            var savedContent = await LoadSavedContentOrFallbackAsync(null, starterContent);
            ApplyDocumentState(null, session.ActiveContent!, savedContent, true);
            SetWorkspaceStatus(static texts => texts.VapourSynthRecoveredDraftStatus);
            return false;
        }

        if (!File.Exists(session.ActiveFilePath))
        {
            ApplyDocumentState(
                null,
                session.ActiveContent!,
                session.ActiveContent!,
                true,
                forceDirtyUntilSave: true);
            SetWorkspaceStatus(texts => texts.VapourSynthRecoveredOrphanedDraftStatus(session.ActiveFilePath));
            return true;
        }

        var savedDocument = await _workspaceService.OpenDocumentAsync(session.ActiveFilePath);
        ApplyDocumentState(session.ActiveFilePath, session.ActiveContent!, savedDocument.Content, true);
        SetWorkspaceStatus(texts => HasExternalFileChanges(session, savedDocument.Content)
            ? texts.VapourSynthRecoveredDraftWithExternalChangesStatus(session.ActiveFilePath)
            : texts.VapourSynthRecoveredDraftStatus);
        return false;
    }

    private string? BuildSavedContentHash()
    {
        return string.IsNullOrWhiteSpace(_currentFilePath)
            ? null
            : ComputeContentHash(_savedContent);
    }

    private static IReadOnlyList<string> NormalizeExistingRecentFiles(System.Collections.Generic.IEnumerable<string>? recentFiles)
    {
        return (recentFiles ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static bool HasExternalFileChanges(VapourSynthWorkspaceSession session, string diskContent)
    {
        return !string.IsNullOrWhiteSpace(session.ActiveSavedContentHash)
            && !string.Equals(
                session.ActiveSavedContentHash,
                ComputeContentHash(NormalizeLineEndings(diskContent)),
                StringComparison.Ordinal);
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 1;
        }

        return content.Count(static character => character == '\n') + 1;
    }

    private static string NormalizeLineEndings(string? content)
    {
        return (content ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string DetectLineEnding(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Environment.NewLine;
        }

        var firstLfIndex = content.IndexOf('\n');
        if (firstLfIndex > 0 && content[firstLfIndex - 1] == '\r')
        {
            return "\r\n";
        }

        if (firstLfIndex >= 0)
        {
            return "\n";
        }

        return content.IndexOf('\r') >= 0 ? "\r" : Environment.NewLine;
    }

    private string RestorePreferredLineEndings(string content)
    {
        var normalized = NormalizeLineEndings(content);
        return _preferredLineEnding == "\n"
            ? normalized
            : normalized.Replace("\n", _preferredLineEnding, StringComparison.Ordinal);
    }

    private void UpdateLogText()
    {
        LogText = _previewLogLines.Count == 0
            ? Texts.VapourSynthLogEmptyPlaceholder
            : string.Join(Environment.NewLine, _previewLogLines);
    }
}

public sealed class VapourSynthRecentFileItem
{
    public VapourSynthRecentFileItem(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        DirectoryPath = Path.GetDirectoryName(fullPath) ?? string.Empty;
        Exists = File.Exists(fullPath);
    }

    public string FullPath { get; }

    public string FileName { get; }

    public string DirectoryPath { get; }

    public bool Exists { get; }

    public Visibility MissingVisibility => Exists ? Visibility.Collapsed : Visibility.Visible;
}
