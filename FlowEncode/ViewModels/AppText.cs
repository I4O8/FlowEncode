using System;
using System.IO;
using FlowEncode.Domain;

namespace FlowEncode.ViewModels;

public sealed class AppText
{
    public AppText(AppLanguage language)
    {
        Language = language;
    }

    public AppLanguage Language { get; }

    public bool IsChinese => Language == AppLanguage.Chinese;

    public string NavDashboard => Pick("工作台", "Dashboard");
    public string NavOverview => Pick("视频压制", "Video Encode");
    public string NavTemplates => Pick("模板库", "Templates");
    public string NavSettings => Pick("设置", "Settings");
    public string NavAutoCompression => Pick("自动压制", "Auto Encode");
    public string NavAudioProcessing => Pick("音频转码", "Audio Transcode");
    public string NavBluRayDemux => Pick("解复用", "Demux");
    public string NavVapourSynthWorkspace => "VapourSynth";
    public string DashboardTitle => Pick("工作台", "Dashboard");
    public string DashboardDescription => Pick(
        "按流程快速进入对应模块：解复用、VapourSynth 脚本编辑与预览、视频压制、模板库、音频转码、自动压制与设置。",
        "Jump straight into each workflow: demux, VapourSynth scripting and preview, video encode, templates, audio transcode, auto encode, and settings.");
    public string DashboardOpenModuleButton => Pick("进入模块", "Open Module");
    public string DashboardDemuxCardDescription => Pick(
        "扫描蓝光目录，选择播放列表与轨道，导出视频、音频和字幕。",
        "Scan a Blu-ray folder, choose a playlist and tracks, then export video, audio, and subtitles.");
    public string DashboardVapourSynthCardDescription => Pick(
        "VapourSynth脚本编辑与预览",
        "VapourSynth script editing and preview");
    public string DashboardVideoEncodeCardDescription => Pick(
        "配置输入输出、编码参数和任务队列，处理常规视频压制任务。",
        "Configure source, output, encoding parameters, and the job queue for standard video encode tasks.");
    public string DashboardTemplatesCardDescription => Pick(
        "管理工作目录中的模板文件，沉淀常用参数并快速复用。",
        "Manage template files in the workspace folder so common parameter sets can be reused quickly.");
    public string DashboardAudioTranscodeCardDescription => Pick(
        "独立处理 FLAC、DDP、Opus 等音频转码流程。",
        "Run standalone audio transcode workflows for FLAC, DDP, Opus, and related formats.");
    public string DashboardAutoCompressionCardDescription => Pick(
        "基于 Av1an 的目标质量流程，自动搜索合适的编码参数。",
        "Use Av1an target-quality mode to search for suitable encoding parameters automatically.");
    public string DashboardSettingsCardDescription => Pick(
        "查看环境与依赖状态，管理主题、语言、更新和首启引导。",
        "Review environment readiness, then manage theme, language, updates, and first-run guidance.");
    public string StatusLabel => Pick("状态", "Status");
    public string ComposerTitle => Pick("新建任务", "New Job");
    public string SourceHeader => Pick("输入源", "Source");
    public string SourcePlaceholder => InputSourceSupport.PlaceholderExamples;
    public string SupportedSourceFileTypeDescription(string pattern) => Pick($"支持的输入文件 ({pattern})", $"Supported source files ({pattern})");
    public string SupportedAudioFileTypeDescription(string pattern) => Pick($"音频文件 ({pattern})", $"Audio files ({pattern})");
    public string AllFilesTypeDescription => Pick("所有文件 (*.*)", "All files (*.*)");
    public string BrowseButton => Pick("浏览", "Browse");
    public string OutputHeader => Pick("输出路径", "Output");
    public string OutputDirectoryHeader => Pick("输出目录", "Output Directory");
    public string SaveAsButton => Pick("另存为", "Save as");
    public string ChooseFolderButton => Pick("选择目录", "Choose Folder");
    public string OutputPreviewPlaceholder => Pick("最终输出：等待输入源和输出目录。", "Final Output: waiting for source and output directory.");
    public string OutputPreviewText(string outputPath) =>
        IsChinese ? $"最终输出：{outputPath}" : $"Final Output: {outputPath}";
    public string QueueButton => Pick("加入队列", "Queue");
    public string QueueAndStartButton => Pick("加入队列并开始", "Queue and Start");
    public string EncoderHeader => Pick("编码器", "Encoder");
    public string RateControlHeader => Pick("码率控制", "Rate Control");
    public string OutputFormatHeader => Pick("输出格式", "Output");
    public string AdditionalArgumentsHeader => Pick("定制压制参数", "Custom Arguments");
    public string UhdArgumentsHeader => Pick("x265 UHD / HDR 附加参数", "x265 UHD / HDR Arguments");
    public string UhdArgumentsPlaceholder => Pick(
        "例如：--master-display \"G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)\"",
        "Example: --master-display \"G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1)\"");
    public string SavedTemplateHeader => Pick("载入已保存模板", "Saved Template");
    public string LoadTemplateButton => Pick("载入模板", "Load Template");
    public string TemplateNameHeader => Pick("模板名称", "Template Name");
    public string TemplateNotesHeader => Pick("模板备注", "Notes");
    public string NotesHeader => Pick("备注", "Notes");
    public string SaveCurrentTemplateButton => Pick("保存当前模板", "Save Template");
    public string SaveCurrentConfigurationButton => Pick("保存当前配置", "Save Current Configuration");
    public string LoadTemplateDialogTitle => Pick("载入模板", "Load Template");
    public string LoadTemplateDialogHeader => Pick("选择模板", "Choose Template");
    public string NoTemplateAvailableMessage => Pick("当前没有可载入的模板。", "No template is available.");
    public string TemplateFileTypeDescription => Pick("FlowEncode 模板 (*.profile)", "FlowEncode Template (*.profile)");
    public string TemplateExportUnavailableMessage => Pick(
        "请先选择一个有效模板并填写模板名称，然后再导出。",
        "Select a valid template and provide a template name before exporting.");
    public string CommandPreviewTitle => Pick("命令预览", "Command Preview");
    public string QueueTitle => Pick("任务队列", "Queue");
    public string QueueTaskColumn => Pick("任务", "Job");
    public string QueueCodecCrfColumn => Pick("编码 / CRF", "Codec / CRF");
    public string QueueStatusColumn => Pick("状态", "Status");
    public string QueueProgressColumn => Pick("进度", "Progress");
    public string EmptyQueue => Pick("暂无任务", "No Jobs");
    public string AutoCompressionTitle => Pick("自动压制 (Av1an)", "Auto Encode (Av1an)");
    public string AutoCompressionDescription => Pick(
        "基于 Av1an 的目标质量模式，按 VMAF 95 自动搜索质量参数。支持 x264、x265、SVT-AV1，并可输入小参（--video-params）。",
        "Use Av1an target-quality mode to automatically search quality settings at VMAF 95. Supports x264, x265, and SVT-AV1 with optional fine params (--video-params).");
    public string AudioProcessingTitle => Pick("音频转码", "Audio Transcode");
    public string AudioProcessingDescription => Pick(
        "独立音频工作流页。用户手动选择转码器后直接执行：eac3to 可输出 FLAC 或 AC3，并支持 -down16 / -mono；deew 用于 DDP；Opus 默认使用 ffmpeg + opusenc，码率由用户在下拉框中手动选择，也可切换到 FFmpeg libopus 并追加 -mapping_family 1。",
        "Dedicated audio workflow page. The app executes the encoder you choose directly: eac3to can output FLAC or AC3 with optional -down16 / -mono, deew handles DDP, and Opus uses ffmpeg plus opusenc by default with a user-selected bitrate, with an optional FFmpeg libopus mode that adds -mapping_family 1.");
    public string BluRayDemuxTitle => Pick("蓝光解复用", "Blu-ray Demux");
    public string BluRayDemuxDescription => Pick(
        "独立蓝光解复用页。默认使用 DGDemux，可手动切换为 eac3to。先扫描蓝光目录，再手动选择播放列表和需要导出的轨道。",
        "Dedicated Blu-ray demux page. DGDemux is the default backend and eac3to is optional. Scan the disc folder first, then choose the playlist and tracks manually.");
    public string BluRayDemuxBackendHeader => Pick("解复用后端", "Backend");
    public string BluRayDemuxScanButton => Pick("扫描蓝光", "Scan Disc");
    public string BluRayPlaylistListTitle => Pick("播放列表", "Playlists");
    public string BluRayTracksTitle => Pick("轨道选择", "Tracks");
    public string BluRaySelectAllTracksButton => Pick("全部勾选", "Select All");
    public string BluRayInvertTracksButton => Pick("反勾选", "Invert");
    public string BluRayDemuxStartButton => Pick("开始解复用", "Start Demux");
    public string BluRayDemuxCancelButton => Pick("取消解复用", "Cancel Demux");
    public string BluRayDemuxClearButton => Pick("清空任务", "Clear Task");
    public string BluRayDemuxStatusTitle => Pick("任务状态", "Task Status");
    public string BluRayDemuxCommandTitle => Pick("解复用命令", "Demux Command");
    public string BluRayDemuxLogTitle => Pick("解复用日志", "Demux Log");
    public string BluRayDemuxCommandPlaceholder => Pick("扫描并选择轨道后，这里会显示最终执行命令。", "The resolved command appears after scanning and selecting tracks.");
    public string BluRayDemuxLogPlaceholder => Pick("开始任务后显示实时日志。", "Live log appears after start.");
    public string BluRayDemuxProgressActiveLabel => Pick("解复用中...", "Demuxing...");
    public string BluRayDemuxAnalyzePhaseLabel => Pick("分析阶段", "Analyze");
    public string BluRayDemuxProcessPhaseLabel => Pick("解复用阶段", "Process");
    public string AudioWorkflowHeader => Pick("转码器", "Encoder");
    public string AudioEac3ToOutputFormatHeader => Pick("目标格式", "Target Format");
    public string AudioEac3ToAdditionalArgumentsHeader => Pick("额外参数", "Extra Arguments");
    public string AudioEac3ToAdditionalArgumentsPlaceholder => Pick("可选：-down16 -mono", "Optional: -down16 -mono");
    public string AudioOpusBitrateHeader => Pick("Opus 码率", "Opus Bitrate");
    public string AudioOpusMappingFamilyHeader => Pick("环绕映射", "Surround Mapping");
    public string AudioOpusMappingFamilyDescription => Pick(
        "兼容的3-8声道使用 Opus family 1；其他布局自动回退默认管线。",
        "Uses Opus family 1 for compatible 3-8 channel layouts; other layouts fall back to the default pipeline.");
    public string AudioChannelProfileHeader => Pick("声道布局", "Channel Layout");
    public string AudioProcessingStartButton => Pick("开始音频处理", "Start Audio");
    public string AudioProcessingCancelButton => Pick("取消音频处理", "Cancel Audio");
    public string AudioProcessingDeleteButton => Pick("删除任务", "Delete Task");
    public string AudioProcessingStatusTitle => Pick("任务状态", "Task Status");
    public string AudioProcessingCommandTitle => Pick("音频命令", "Audio Command");
    public string AudioProcessingLogTitle => Pick("音频日志", "Audio Log");
    public string AudioProcessingCommandPlaceholder => Pick("输入源与工作流就绪后，这里会显示最终执行命令。", "The resolved command appears when the source and workflow are ready.");
    public string AudioProcessingLogPlaceholder => Pick("开始任务后显示实时日志。", "Live log appears after start.");
    public string AudioProcessingProgressActiveLabel => Pick("处理中...", "Working...");
    public string AudioProcessingDdpWarmupHint => Pick("ffmpeg 处理中，请稍后...", "FFmpeg is preparing the source. Please wait...");
    public string AudioProcessingProgressIndeterminateHint => Pick("当前阶段暂无法稳定计算百分比，任务仍在执行。", "This stage has no reliable percentage yet. The task is still running.");
    public string AutoCompressionTargetVmafHeader => "VMAF";
    public string AutoCompressionProbesHeader => Pick("探测次数", "Probes");
    public string AutoCompressionWorkersHeader => Pick("并行任务数", "Workers");
    public string AutoCompressionSmallParametersHeader => Pick("小参（--video-params）", "Fine Params (--video-params)");
    public string AutoCompressionStartButton => Pick("开始自动压制", "Start Auto Encode");
    public string AutoCompressionCancelButton => Pick("取消自动压制", "Cancel Auto Encode");
    public string AutoCompressionStatusTitle => Pick("任务状态", "Task Status");
    public string AutoCompressionCommandTitle => Pick("Av1an 命令", "Av1an Command");
    public string AutoCompressionLogTitle => Pick("Av1an 日志", "Av1an Log");
    public string AutoCompressionCommandPlaceholder => Pick("开始任务后显示最终执行命令。", "The resolved command appears after start.");
    public string AutoCompressionLogPlaceholder => Pick("开始任务后显示实时日志。", "Live log appears after start.");
    public string AutoCompressionProgressActiveLabel => Pick("工作中...", "Working...");
    public string AutoCompressionProgressIndeterminateHint => Pick("当前阶段暂无法计算百分比，任务仍在执行。", "This stage has no reliable percentage yet. The task is still running.");
    public string JobDetailsTitle => Pick("任务详情", "Details");
    public string TemplateLibraryTitle => Pick("模板库", "Template Library");
    public string TemplateSearchPlaceholder => Pick("搜索模板名称", "Search templates");
    public string TemplateNoMatch => Pick("未找到匹配模板", "No matching templates");
    public string TemplateEditorTitle => Pick("模板编辑", "Template Editor");
    public string VapourSynthWorkspaceTitle => "VapourSynth";
    public string VapourSynthWorkspaceDescription => Pick(
        "脚本工作区负责 .vpy / .py 的集中编辑、F5 预览窗口、脚本评估与预览日志。这里仍然明确排除音频相关能力。",
        "The script workspace handles focused .vpy / .py editing, the F5 preview window, and script evaluation plus preview logs. Audio-related features remain explicitly out of scope here.");
    public string VapourSynthOpenButton => Pick("打开", "Open");
    public string VapourSynthReloadButton => Pick("重新载入", "Reload");
    public string VapourSynthFindButton => Pick("查找", "Find");
    public string VapourSynthReplaceButton => Pick("替换", "Replace");
    public string VapourSynthGoToButton => Pick("跳转行", "Go to Line");
    public string VapourSynthUndoButton => Pick("撤销", "Undo");
    public string VapourSynthRedoButton => Pick("重做", "Redo");
    public string VapourSynthPreviewButton => Pick("预览 F5", "Preview F5");
    public string VapourSynthLogTitle => "Log";
    public string VapourSynthClearLogButton => Pick("清空日志", "Clear Log");
    public string VapourSynthLogEmptyPlaceholder => Pick(
        "预览和脚本评估日志会显示在这里。",
        "Preview and script evaluation logs appear here.");
    public string VapourSynthRecentFilesTitle => Pick("最近文件", "Recent Files");
    public string VapourSynthRecentFilesDescription => Pick(
        "快速回到最近打开的脚本。缺失文件会在打开时提示并从列表中移除。",
        "Jump back into recently opened scripts. Missing files are reported and removed from the list when opened.");
    public string VapourSynthRecentFilesEmpty => Pick("还没有最近打开的脚本。", "No recently opened scripts yet.");
    public string VapourSynthShortcutsTitle => Pick("快捷键", "Shortcuts");
    public string VapourSynthShortcutLegend => Pick(
        "Ctrl+N 新建\nCtrl+O 打开\nCtrl+S 保存\nCtrl+Shift+S 另存为\nCtrl+F 查找\nCtrl+H 替换\nCtrl+G 跳转行\nCtrl+/ 注释切换\nF5 预览入口",
        "Ctrl+N new\nCtrl+O open\nCtrl+S save\nCtrl+Shift+S save as\nCtrl+F find\nCtrl+H replace\nCtrl+G go to line\nCtrl+/ toggle comment\nF5 preview entry");
    public string VapourSynthUntitledDocument => "untitled.vpy";
    public string VapourSynthPathPlaceholder => Pick("尚未保存到磁盘。", "Not saved to disk yet.");
    public string VapourSynthModifiedBadge => Pick("未保存修改", "Modified");
    public string VapourSynthFileTypeDescription => Pick("VapourSynth 脚本", "VapourSynth Script");
    public string VapourSynthPythonFileTypeDescription => Pick("Python 脚本", "Python Script");
    public string VapourSynthEditorLoadingStatus => Pick("正在初始化 VapourSynth 编辑器...", "Initializing the VapourSynth editor...");
    public string VapourSynthEditorReadyStatus => Pick("编辑器已就绪。", "Editor ready.");
    public string VapourSynthEditorBootingStatus => Pick("正在启动编辑器内核，请稍候...", "Starting the editor runtime. Please wait...");
    public string VapourSynthEditorTimeoutStatus => Pick(
        "编辑器前端没有按预期启动。可以重试一次；如果仍失败，再继续排查 WebView2 宿主。",
        "The editor frontend did not start as expected. Retry once first, then keep debugging the WebView2 host if it still fails.");
    public string VapourSynthEditorRetryButton => Pick("重试编辑器", "Retry Editor");
    public string VapourSynthNewDocumentStatus => Pick("已创建新的脚本缓冲区。", "Created a new script buffer.");
    public string VapourSynthRecoveredDraftStatus => Pick("已恢复上次未保存的脚本草稿。", "Recovered the unsaved draft from the previous session.");
    public string VapourSynthRecoveredDraftWithExternalChangesStatus(string filePath) =>
        IsChinese
            ? $"已恢复上次未保存的脚本草稿，但磁盘文件已被外部修改：{filePath}"
            : $"Recovered the unsaved draft, but the on-disk file was modified externally: {filePath}";
    public string VapourSynthRecoveredOrphanedDraftStatus(string filePath) =>
        IsChinese
            ? $"上次会话对应的脚本已经不存在，已恢复未保存草稿。请确认内容后重新保存：{filePath}"
            : $"The previous session file no longer exists. The unsaved draft was recovered and now needs a new save target: {filePath}";
    public string VapourSynthOpenedStatus(string filePath) =>
        IsChinese ? $"已打开脚本：{filePath}" : $"Opened script: {filePath}";
    public string VapourSynthSavedStatus(string filePath) =>
        IsChinese ? $"已保存脚本：{filePath}" : $"Saved script: {filePath}";
    public string VapourSynthReloadedStatus(string filePath) =>
        IsChinese ? $"已从磁盘重新载入：{filePath}" : $"Reloaded from disk: {filePath}";
    public string VapourSynthRestoredDocumentStatus(string filePath) =>
        IsChinese ? $"已恢复上次会话的脚本：{filePath}" : $"Restored script from the previous session: {filePath}";
    public string VapourSynthRestoredUpdatedDocumentStatus(string filePath) =>
        IsChinese ? $"已恢复上次会话的脚本，并加载磁盘上的最新内容：{filePath}" : $"Restored the previous session script and loaded the latest on-disk content: {filePath}";
    public string VapourSynthRestoredMissingFileStatus(string filePath) =>
        IsChinese ? $"上次会话脚本已经不存在，已改为新建脚本缓冲区：{filePath}" : $"The previous session script no longer exists, so a new script buffer was opened instead: {filePath}";
    public string VapourSynthLaunchFileMissingStatus(string filePath) =>
        IsChinese ? $"系统传入的脚本不存在，已改为新建脚本缓冲区：{filePath}" : $"The script passed in from the shell no longer exists, so a new script buffer was opened instead: {filePath}";
    public string VapourSynthLaunchOpenFailedStatus(string filePath, string detail) =>
        IsChinese ? $"无法打开系统传入的脚本：{filePath}。{detail}" : $"Failed to open the script passed in from the shell: {filePath}. {detail}";
    public string VapourSynthEditorCursorStatus(int line, int column, int lineCount, int charCount, bool isDirty) =>
        IsChinese
            ? $"第 {line} 行，第 {column} 列  |  {lineCount} 行  |  {charCount} 字符  |  {(isDirty ? "未保存" : "已保存")}"
            : $"Ln {line}, Col {column}  |  {lineCount} lines  |  {charCount} chars  |  {(isDirty ? "modified" : "saved")}";
    public string VapourSynthUnsavedChangesTitle => Pick("保存脚本修改", "Save Script Changes");
    public string VapourSynthUnsavedChangesMessage => Pick("当前脚本有未保存的修改。是否先保存再继续？", "The current script has unsaved changes. Save before continuing?");
    public string VapourSynthPreviewDeferredTitle => Pick("预览窗口待实现", "Preview Window Pending");
    public string VapourSynthPreviewDeferredMessage => Pick(
        "第一步只落编辑器本体。F5 预览窗口和完整交互会在第二步单独接入。",
        "Step one only ships the editor. The F5 preview window and its full interaction model land in step two.");
    public string VapourSynthPreviewWindowTitle(string documentName) =>
        IsChinese ? $"{documentName} - 预览" : $"{documentName} - Preview";
    public string VapourSynthPreviewIdleStatus => Pick("预览窗口待命。", "Preview is idle.");
    public string VapourSynthPreviewUnknownTime => "--:--.---";
    public string VapourSynthPreviewFramePropsPlaceholder => Pick(
        "当前还没有帧属性。载入脚本并渲染帧后，这里会显示对应的 Frame Props。",
        "Frame props are not available yet. Render a frame to inspect its metadata here.");
    public string VapourSynthPreviewFramePropsEmpty => Pick(
        "当前帧没有可显示的属性。",
        "The current frame does not expose any properties.");
    public string VapourSynthPreviewZoomFitLabel => Pick("适应窗口", "Fit");
    public string VapourSynthPreviewZoomActualLabel => Pick("实际大小", "Actual");
    public string VapourSynthPreviewZoomCustomLabel => Pick("自定义", "Custom");
    public string VapourSynthPreviewOutputLabel(int index, string name)
    {
        var fallbackLabel = Pick($"输出 {index}", $"Output {index}");
        if (string.IsNullOrWhiteSpace(name)
            || string.Equals(name, fallbackLabel, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, $"Output {index}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, $"输出 {index}", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackLabel;
        }

        return $"{fallbackLabel} · {name}";
    }
    public string VapourSynthPreviewOutputHeader => Pick("输出", "Output");
    public string VapourSynthPreviewFrameHeader => Pick("帧", "Frame");
    public string VapourSynthPreviewZoomHeader => Pick("缩放", "Zoom");
    public string VapourSynthPreviewFramePropsButton => Pick("帧属性", "Frame Props");
    public string VapourSynthPreviewReturnToEditorButton => Pick("返回编辑器", "Back To Editor");
    public string VapourSynthPreviewExitButton => Pick("退出预览", "Exit Preview");
    public string VapourSynthPreviewFramePropsPaneTitle => Pick("Frame Props", "Frame Props");
    public string VapourSynthPreviewSaveFrameButton => Pick("保存帧", "Save Frame");
    public string VapourSynthPreviewCopyFrameButton => Pick("复制帧", "Copy Frame");
    public string VapourSynthPreviewInsertFrameButton => Pick("插入帧号", "Insert Frame");
    public string VapourSynthPreviewAdvancedSettingsButton => Pick("高级设置", "Advanced");
    public string VapourSynthPreviewTimelineButton => Pick("时间线", "Timeline");
    public string VapourSynthPreviewCropButton => Pick("裁剪助手", "Crop");
    public string VapourSynthPreviewStepHeader => Pick("步长", "Step");
    public string VapourSynthPreviewFirstFrameButton => Pick("第一帧", "First Frame");
    public string VapourSynthPreviewPreviousFrameButton => Pick("上一帧", "Previous Frame");
    public string VapourSynthPreviewPlayPauseButton => Pick("播放/暂停", "Play/Pause");
    public string VapourSynthPreviewNextFrameButton => Pick("下一帧", "Next Frame");
    public string VapourSynthPreviewLastFrameButton => Pick("最后一帧", "Last Frame");
    public string VapourSynthPreviewTimeStepBackButton => Pick("后退时间步长", "Step Back");
    public string VapourSynthPreviewTimeStepForwardButton => Pick("前进时间步长", "Step Forward");
    public string VapourSynthPreviewPrevOutputButton => Pick("上一输出", "Previous Output");
    public string VapourSynthPreviewNextOutputButton => Pick("下一输出", "Next Output");
    public string VapourSynthPreviewTimelinePaneTitle => Pick("时间线", "Timeline");
    public string VapourSynthPreviewTimelineModeHeader => Pick("显示模式", "Display");
    public string VapourSynthPreviewTimelineFramesMode => Pick("帧", "Frames");
    public string VapourSynthPreviewTimelineTimeMode => Pick("时间", "Time");
    public string VapourSynthPreviewTimeStepHeader => Pick("时间步长（秒）", "Time Step (s)");
    public string VapourSynthPreviewCropPaneTitle => Pick("裁剪助手", "Crop Assistant");
    public string VapourSynthPreviewCropModeHeader => Pick("裁剪模式", "Crop Mode");
    public string VapourSynthPreviewCropAbsoluteMode => Pick("绝对", "Absolute");
    public string VapourSynthPreviewCropRelativeMode => Pick("相对", "Relative");
    public string VapourSynthPreviewCropLeftHeader => Pick("左", "Left");
    public string VapourSynthPreviewCropTopHeader => Pick("上", "Top");
    public string VapourSynthPreviewCropWidthHeader => Pick("宽", "Width");
    public string VapourSynthPreviewCropHeightHeader => Pick("高", "Height");
    public string VapourSynthPreviewCropRightHeader => Pick("右", "Right");
    public string VapourSynthPreviewCropBottomHeader => Pick("下", "Bottom");
    public string VapourSynthPreviewCropZoomHeader => Pick("裁剪缩放 (%)", "Crop Zoom (%)");
    public string VapourSynthPreviewCopyCropCommandButton => Pick("复制裁剪命令", "Copy Crop");
    public string VapourSynthPreviewPasteCropCommandButton => Pick("插入裁剪命令", "Insert Crop");
    public string VapourSynthPreviewAdvancedSettingsTitle => Pick("预览高级设置", "Preview Advanced Settings");
    public string VapourSynthPreviewOutputSyncHeader => Pick("输出同步", "Output Sync");
    public string VapourSynthPreviewOutputSyncRemember => Pick("记住各输出状态", "Remember Per Output");
    public string VapourSynthPreviewOutputSyncFrame => Pick("按帧同步", "Sync by Frame");
    public string VapourSynthPreviewOutputSyncTimestamp => Pick("按时间戳同步", "Sync by Timestamp");
    public string VapourSynthPreviewOutputSyncTimeline => Pick("跟随时间线模式", "Follow Timeline Mode");
    public string VapourSynthPreviewSilentSnapshotHeader => Pick("静默保存快照", "Silent Snapshot");
    public string VapourSynthPreviewSnapshotTemplateHeader => Pick("快照模板", "Snapshot Template");
    public string VapourSynthPreviewSnapshotTemplateHint => Pick(
        "可用占位符：{scriptName} {output} {frame} {time} {ext}。",
        "Placeholders: {scriptName} {output} {frame} {time} {ext}.");
    public string VapourSynthPreviewAdvancedSettingsSavedStatus => Pick("预览高级设置已更新。", "Preview advanced settings updated.");
    public string VapourSynthPreviewEvaluatingStatus(string documentName) =>
        IsChinese ? $"正在求值脚本：{documentName}" : $"Evaluating script: {documentName}";
    public string VapourSynthPreviewSessionReadyStatus => Pick(
        "脚本已求值，可以开始预览。",
        "The script is ready for preview.");
    public string VapourSynthPreviewOpenedStatus(string documentName) =>
        IsChinese ? $"预览已打开：{documentName}" : $"Preview opened: {documentName}";
    public string VapourSynthPreviewRenderingStatus(int outputIndex, int frameNumber) =>
        IsChinese
            ? $"正在渲染输出 {outputIndex} 的第 {frameNumber} 帧..."
            : $"Rendering output {outputIndex}, frame {frameNumber}...";
    public string VapourSynthPreviewReadyStatus(int outputIndex, int frameNumber) =>
        IsChinese
            ? $"输出 {outputIndex} 的第 {frameNumber} 帧已就绪。"
            : $"Output {outputIndex}, frame {frameNumber} is ready.";
    public string VapourSynthPreviewRenderFailedStatus(string detail) =>
        IsChinese ? $"预览失败：{detail}" : $"Preview failed: {detail}";
    public string VapourSynthPreviewEvaluationFailedStatus => Pick(
        "脚本求值失败，预览未打开。详见下方日志。",
        "Script evaluation failed. Preview was not opened. See the log below.");
    public string VapourSynthPreviewPlaybackStatus => Pick("正在播放预览。", "Playing preview.");
    public string VapourSynthPreviewSnapshotFileTypeDescription => Pick("PNG 图像", "PNG Image");
    public string VapourSynthPreviewSnapshotSavedStatus(string filePath) =>
        IsChinese ? $"已保存当前帧：{filePath}" : $"Saved current frame: {filePath}";
    public string VapourSynthPreviewFrameCopiedStatus => Pick("已复制当前帧到剪贴板。", "Copied the current frame to the clipboard.");
    public string VapourSynthPreviewFrameNumberInsertedStatus => Pick("已把当前帧号插入脚本。", "Inserted the current frame number into the script.");
    public string VapourSynthPreviewCropSnippetInsertedStatus => Pick("已把裁剪命令插入脚本。", "Inserted the crop command into the script.");
    public string VapourSynthPreviewCropCommandCopiedStatus => Pick("已复制裁剪命令。", "Copied the crop command.");
    public string VapourSynthPreviewEditorBridgeUnavailableStatus => Pick(
        "编辑器当前不可用，无法回写脚本。",
        "The editor is unavailable, so the script could not be updated.");
    public string VapourSynthPreviewSnapshotTemplateFailedStatus(string detail) =>
        IsChinese ? $"快照模板不可用，已切回手动保存：{detail}" : $"The snapshot template is invalid. Falling back to manual save: {detail}";
    public string VapourSynthMissingRecentFileMessage(string filePath) =>
        IsChinese ? $"最近文件已经不存在：{filePath}" : $"The recent file no longer exists: {filePath}";
    public string VapourSynthEditorAssetsMissingStatus(string assetPath) =>
        IsChinese ? $"未找到编辑器前端资源：{assetPath}" : $"Editor frontend assets were not found: {assetPath}";
    public string VapourSynthEditorLoadFailedStatus(string detail) =>
        IsChinese ? $"编辑器宿主初始化失败：{detail}" : $"Failed to initialize the editor host: {detail}";
    public string VapourSynthEditorBridgeFailedStatus(string detail) =>
        IsChinese ? $"编辑器桥接异常：{detail}" : $"Editor bridge error: {detail}";
    public string VapourSynthLanguageRuntimeUnavailableStatus(string detail) =>
        IsChinese ? $"编辑器已启动，但 VapourSynth 语言功能不可用：{detail}" : $"The editor started, but VapourSynth language features are unavailable: {detail}";
    public string VapourSynthPythonLanguageServerUnavailableStatus(string detail) =>
        IsChinese ? $"编辑器已启动，但 Python 语言服务不可用：{detail}" : $"The editor started, but Python language service is unavailable: {detail}";
    public string NewButton => Pick("新建", "New");
    public string SaveButton => Pick("保存", "Save");
    public string SettingsTitle => Pick("设置", "Settings");
    public string ThemeHeader => Pick("主题", "Theme");
    public string LanguageHeader => Pick("语言", "Language");
    public string ToggleOnLabel => Pick("开", "On");
    public string ToggleOffLabel => Pick("关", "Off");
    public string PreferSystemEncodersHeader => Pick("本地缺失时回退系统编码器", "Fallback to system encoders when local is missing");
    public string AutoCheckUpdatesHeader => Pick("启动时自动检查程序更新", "Check app updates on startup");
    public string MaxConcurrentEncodingJobsHeader => Pick("同时编码任务数", "Concurrent Encode Jobs");
    public string AppUpdateSectionTitle => Pick("程序更新", "App Updates");
    public string CheckUpdatesButton => Pick("检查更新", "Check for Updates");
    public string CheckingUpdatesButton => Pick("检查中...", "Checking...");
    public string DownloadingUpdateButton => Pick("下载中...", "Downloading...");
    public string DownloadingUpdateButtonWithProgress(int progressPercent) =>
        IsChinese
            ? $"下载中 {progressPercent}%"
            : $"Downloading {progressPercent}%";
    public string ToolDirectoryButton => Pick("工作目录", "Workspace Folder");
    public string WorkspaceDirectoryHeader => Pick("工作目录", "Workspace Folder");
    public string WorkspaceDirectoryDescription => Pick(
        "大体积运行内容会落到这里，包括 downloads、tools、encoders，以及 Templates 模板目录。轻量设置、日志和首启引导缓存仍保留在 %LocalAppData%\\FlowEncode\\data。",
        "Large runtime content lives here, including downloads, tools, encoders, and the Templates folder. Lightweight settings, logs, and setup-guide cache stay under %LocalAppData%\\FlowEncode\\data.");
    public string WorkspaceDirectoryRestartHint => Pick(
        "修改工作目录后需要重启程序，新目录才会正式接管后续下载和托管依赖。",
        "Restart the app after changing the workspace folder so future downloads and managed dependencies switch to the new location.");
    public string SetupGuideButton => Pick("首启引导", "Setup Guide");
    public string SetupGuideTitle => Pick("首次启动环境引导", "First-run Environment Guide");
    public string SetupGuideDescription => Pick(
        "按 Python -> VapourSynth -> 插件/运行时 -> 编码器与命令行工具 的顺序检查。安装 Python 和 VapourSynth 后，还必须继续安装指定的 VS 插件和 Python 包。引导层不是普通页面，后续可在设置里随时重新打开。",
        "Check readiness in this order: Python -> VapourSynth -> plugins/runtime -> encoders and CLI tools. Installing Python and VapourSynth is still not enough by itself; the required VS packages and Python modules must also be installed. This guide is an overlay, not a normal page, and can be reopened from Settings.");
    public string SetupGuideSwipeHint => Pick("可左右滑动浏览卡片，也可以使用左右按钮切换。", "Swipe left or right to browse cards, or use the navigation buttons.");
    public string SetupGuideRefreshButton => Pick("重新检测", "Recheck");
    public string SetupGuideRefreshingButton => Pick("检测中...", "Checking...");
    public string SetupGuideCheckUpdatesButton => Pick("检查依赖更新", "Check Dependency Updates");
    public string SetupGuideCloseButton => Pick("进入工作台", "Enter Workspace");
    public string SetupGuidePreviousButton => Pick("上一张", "Previous");
    public string SetupGuideNextButton => Pick("下一张", "Next");
    public string SetupGuideLocalCheckIdleStatus => Pick("本地环境状态：尚未检测。", "Local environment status has not been checked.");
    public string SetupGuideRemoteCheckIdleStatus => Pick("依赖更新：尚未检查。", "Dependency updates have not been checked.");
    public string SetupGuideLocalRefreshCompletedStatus => Pick("本地环境状态已更新。", "Local environment status refreshed.");
    public string SetupGuideLocalCheckedAtLabel(DateTimeOffset time) =>
        IsChinese
            ? $"本地检测：{time.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            : $"Local check: {time.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

    public string SetupGuideRemoteCheckedAtLabel(DateTimeOffset time) =>
        IsChinese
            ? $"依赖更新：{time.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
            : $"Dependency updates: {time.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    public string SetupGuidePortableSectionTitle => Pick("工作目录布局", "Workspace Layout");
    public string SetupGuidePortableSectionDescription => Pick(
        "托管工具、下载缓存、编码器和 Templates 模板目录位于工作目录；轻量设置、日志和首启引导缓存仍保留在 %LocalAppData%\\FlowEncode\\data。",
        "Managed tools, download cache, encoders, and the Templates folder live in the workspace folder, while lightweight settings, logs, and setup-guide cache stay under %LocalAppData%\\FlowEncode\\data.");
    public string AppFolderButton => Pick("工作目录", "Workspace Folder");
    public string EncodersFolderButton => Pick("编码器目录", "Encoders Folder");
    public string ToolsFolderButton => Pick("工具目录", "Tools Folder");
    public string SettingsFolderButton => Pick("设置目录", "Settings Folder");
    public string TemplatesFolderButton => Pick("模板目录", "Templates Folder");
    public string DownloadsFolderButton => Pick("下载目录", "Downloads Folder");
    public string LocalizationFolderButton => Pick("本地化目录", "Localization Folder");
    public string OpenDocumentationButton => Pick("打开文档", "Open Docs");
    public string EnvironmentSectionTitle => Pick("环境能力", "Environment");
    public string EncoderSectionTitle => Pick("编码器", "Encoders");
    public string ToolSectionTitle => Pick("核心工具", "Core Tools");
    public string ManualSelectButton => Pick("手动选择", "Choose");
    public string ClearManualPinButton => Pick("取消固定", "Clear Pin");
    public string ManualPinnedLabel => Pick("手动固定", "Manual Pin");
    public string ManualToolPinnedStatus(string dependencyLabel) =>
        IsChinese
            ? $"{dependencyLabel} 已固定为手动选择路径。"
            : $"{dependencyLabel} was pinned to the selected path.";
    public string ManualToolPinClearedStatus(string dependencyLabel) =>
        IsChinese
            ? $"{dependencyLabel} 已取消手动固定。"
            : $"{dependencyLabel} manual pin was cleared.";
    public string ManualToolSelectUnsupported => Pick("当前依赖不支持手动选择可执行文件。", "This dependency does not support manual executable selection.");
    public string ManualToolInvalidSelection => Pick("请选择有效的可执行文件。", "Choose a valid executable file.");
    public string ManualToolUpdateOverrideTitle => Pick("覆盖手动固定路径", "Override Manual Pin");
    public string ManualToolUpdateOverrideMessage(string dependencyLabel) =>
        IsChinese
            ? $"{dependencyLabel} 当前使用手动固定路径。继续更新会取消手动固定，并改用程序自动安装/更新的版本。是否继续？"
            : $"{dependencyLabel} currently uses a manually pinned path. Continuing will clear the manual pin and use the app-managed updated version. Continue?";
    public string ImportButton => Pick("导入", "Import");
    public string ExportButton => Pick("导出", "Export");
    public string InstallButton => Pick("安装", "Install");
    public string UpdateButton => Pick("更新", "Update");
    public string UninstallButton => Pick("卸载", "Uninstall");
    public string ReleasePageButton => Pick("发布页", "Release Page");
    public string EnvironmentAndDependenciesTitle => Pick("环境与依赖", "Environment & Dependencies");
    public string TemplateSourceUser => Pick("用户", "User");
    public string TemplateSourcePinned => Pick("置顶", "Pinned");
    public string PinTemplateButton => Pick("置顶模板", "Pin Template");
    public string UnpinTemplateButton => Pick("取消置顶", "Unpin Template");
    public string DeleteTemplateButton => Pick("删除模板", "Delete Template");
    public string JobMenuStart => Pick("开始", "Start");
    public string JobMenuPrioritize => Pick("优先执行", "Start Next");
    public string JobMenuMoveTop => Pick("移到顶部", "Move to Top");
    public string JobMenuMoveUp => Pick("上移一位", "Move Up");
    public string JobMenuMoveDown => Pick("下移一位", "Move Down");
    public string JobMenuMoveBottom => Pick("移到队尾", "Move to Bottom");
    public string JobMenuCancel => Pick("取消任务", "Cancel Job");
    public string JobMenuRestart => Pick("重启任务", "Restart Job");
    public string JobMenuDelete => Pick("删除任务", "Delete Job");
    public string OkButton => Pick("确定", "OK");
    public string OverwriteButton => Pick("覆盖", "Overwrite");
    public string CancelButton => Pick("取消", "Cancel");
    public string DontSaveButton => Pick("不保存", "Don't Save");
    public string DefaultProgressPrimary => "0%   0/? frames";
    public string DefaultProgressSecondary => Pick("预计剩余 --:--:--   预计大小 --", "eta --:--:--   est. size --");
    public string EtaPrefix => Pick("预计剩余", "eta");
    public string EstimatedSizePrefix => Pick("预计大小", "est. size");
    public string SvtAv1TwoPassOverlayConflict =>
        Pick(
            "SVT-AV1 的 2-Pass 与 `--enable-overlays 1` 冲突。请在定制压制参数中移除该项，或改为 `--enable-overlays 0`。",
            "SVT-AV1 2-pass conflicts with `--enable-overlays 1`. Remove it from custom arguments or change it to `--enable-overlays 0`.");
    public string DescribeArgumentConflict(EncoderArgumentConflict conflict)
    {
        return conflict.Kind switch
        {
            EncoderArgumentConflictKind.OppositeSwitches => DescribeOppositeSwitchConflict(conflict),
            _ => DescribeConflictingValueConflict(conflict)
        };
    }

    public string NotStarted => Pick("未开始", "Not started");
    public string QueuePreparing => Pick("环境已准备完成，等待首次刷新。", "Environment ready. Waiting for first refresh.");
    public string AutoCompressionIdleStatus => Pick("等待自动压制任务。", "Waiting for an auto-encode task.");
    public string AudioProcessingIdleStatus => Pick("等待音频处理任务。", "Waiting for an audio task.");
    public string BluRayDemuxIdleStatus => Pick("等待蓝光解复用任务。", "Waiting for a Blu-ray demux task.");
    public string BluRayDiscSummaryPlaceholder => Pick("选择蓝光目录后手动点击扫描。", "Choose a Blu-ray folder, then scan manually.");
    public string BluRayPlaylistSummaryPlaceholder => Pick("扫描完成后，手动选择一条播放列表以加载轨道。", "After scanning, choose a playlist manually to load tracks.");
    public string AudioSourceInfoPlaceholder => Pick("选择音频源后，程序会尝试用 FFprobe 自动读取声道、位深和采样率。", "After you choose a source, FFprobe will try to read channel, bit-depth, and sample-rate info automatically.");
    public string AudioSourceInspectingStatus => Pick("正在分析音频源信息。", "Inspecting audio source information.");
    public string AudioCapabilityPreparing => Pick("正在准备音频工作流依赖状态。", "Preparing audio workflow readiness.");
    public string InitialPreviewTitle => Pick("选择一个预设以生成命令预览", "Select a preset to preview the command");
    public string InitialPreviewNotes => Pick("预览命令会围绕后续的作业队列和滤镜管线展开。", "The preview command reflects the queue workflow and filter pipeline.");
    public string NoProfileSelectedCaption => Pick("尚未选择预设", "No preset selected");
    public string SuggestedOutputName => "flowencode_output";
    public string DraftSuffix => Pick("草稿", "Draft");
    public string ManualDraftCaption => Pick("当前草稿 · 手动配置", "Current Draft · Manual");
    public string NewTemplateCaption => Pick("新建模板", "New Template");
    public string DraftNotReadyTitle => Pick("当前草稿未就绪", "Current draft is not ready");
    public string DraftNotReadyNotes => Pick("先选择编码器，并完成当前作业的关键参数。", "Select an encoder and finish the key parameters for this job first.");
    public string SelectJobForCommandText => Pick("从任务列表中选择一个作业后，这里会显示实际执行命令。", "Select a job to view the actual command.");
    public string SelectJobForLogText => Pick("从任务列表中选择一个作业后，这里会显示关键编码日志。", "Select a job to view the key encoding log.");
    public string NoSelectedJobLogText => Pick("当前还没有可显示的关键日志。", "No key log is available yet.");
    public string SelectedJobSummaryPlaceholder => Pick("从队列中选择一个作业后，这里会显示完整命令和实时日志。", "Select a job from the queue to view the full command and live log.");
    public string EnvironmentStatePreparing => Pick("正在准备环境能力状态。", "Preparing environment readiness.");
    public string NoQueueJobs => Pick("暂无编码作业。", "No encoding jobs.");
    public string InitializationStatus => Pick("开发环境验证通过，已切换到 WinUI 3 开发流。", "Environment verified. WinUI 3 workflow is ready.");
    public string SettingsSavedStatus => Pick("设置已保存。", "Settings saved.");
    public string WorkspaceDirectoryPreparingStatus => Pick("正在准备新的工作目录...", "Preparing the new workspace folder...");
    public string WorkspaceDirectorySavedStatus => Pick("工作目录已保存，重启后生效。", "Workspace folder saved. Restart the app to apply it.");
    public string WorkspaceDirectoryChangeBlockedMessage => Pick(
        "当前仍有进行中的任务或依赖操作。请先停止这些动作，再调整工作目录。",
        "A job or dependency operation is still running. Stop it before changing the workspace folder.");
    public string WorkspaceDirectoryInvalidLocationMessage => Pick(
        "工作目录不能放在安装目录或 Program Files 下。请选择一个普通可写目录。",
        "The workspace folder cannot live inside the install directory or Program Files. Choose a normal writable folder instead.");
    public string UserTemplateDeletedStatus => Pick("用户模板已删除。", "User template deleted.");
    public string CloseRunningJobsTitle => Pick("确认关闭程序", "Confirm Close");
    public string CloseRunningJobsButton => Pick("关闭程序", "Close App");
    public string CloseRunningJobsMessage(int count) =>
        IsChinese
            ? $"当前仍有 {count} 个任务正在编码。关闭程序会终止对应编码进程，并使这些任务失败。是否确认关闭？"
            : $"{count} encoding job(s) are still running. Closing the app will terminate the encoder processes and fail those jobs. Close anyway?";
    public string CloseRunningWorkMessage(int runningJobCount, bool autoCompressionRunning, bool audioProcessingRunning, bool bluRayDemuxRunning)
    {
        if (autoCompressionRunning && audioProcessingRunning && bluRayDemuxRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务、1 个自动压制任务、1 个音频处理任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s), 1 auto-encode task, 1 audio task, and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning && audioProcessingRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务、1 个自动压制任务和 1 个音频处理任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s), 1 auto-encode task, and 1 audio task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning && bluRayDemuxRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务、1 个自动压制任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s), 1 auto-encode task, and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (audioProcessingRunning && bluRayDemuxRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务、1 个音频处理任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s), 1 audio task, and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务和 1 个自动压制任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s) and 1 auto-encode task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (audioProcessingRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务和 1 个音频处理任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s) and 1 audio task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (bluRayDemuxRunning && runningJobCount > 0)
        {
            return IsChinese
                ? $"当前仍有 {runningJobCount} 个普通压制任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : $"{runningJobCount} regular encoding job(s) and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning && audioProcessingRunning)
        {
            return IsChinese
                ? "当前仍有 1 个自动压制任务和 1 个音频处理任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : "1 auto-encode task and 1 audio task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning && bluRayDemuxRunning)
        {
            return IsChinese
                ? "当前仍有 1 个自动压制任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : "1 auto-encode task and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (audioProcessingRunning && bluRayDemuxRunning)
        {
            return IsChinese
                ? "当前仍有 1 个音频处理任务和 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使这些任务失败。是否确认关闭？"
                : "1 audio task and 1 Blu-ray demux task are still running. Closing the app will terminate their processes and fail these runs. Close anyway?";
        }

        if (autoCompressionRunning)
        {
            return IsChinese
                ? "当前仍有 1 个自动压制任务在运行。关闭程序会终止对应进程，并使该任务失败。是否确认关闭？"
                : "1 auto-encode task is still running. Closing the app will terminate its process and fail this run. Close anyway?";
        }

        if (audioProcessingRunning)
        {
            return IsChinese
                ? "当前仍有 1 个音频处理任务在运行。关闭程序会终止对应进程，并使该任务失败。是否确认关闭？"
                : "1 audio task is still running. Closing the app will terminate its process and fail this run. Close anyway?";
        }

        if (bluRayDemuxRunning)
        {
            return IsChinese
                ? "当前仍有 1 个蓝光解复用任务在运行。关闭程序会终止对应进程，并使该任务失败。是否确认关闭？"
                : "1 Blu-ray demux task is still running. Closing the app will terminate its process and fail this run. Close anyway?";
        }

        return CloseRunningJobsMessage(runningJobCount);
    }
    public string OverwriteOutputTitle => Pick("输出文件已存在", "Output File Exists");
    public string OverwriteOutputMessage(string path) =>
        IsChinese
            ? $"输出文件已存在：{Path.GetFileName(path)}{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}是否继续？"
            : $"The output file already exists: {Path.GetFileName(path)}{Environment.NewLine}{path}{Environment.NewLine}{Environment.NewLine}Continue?";
    public string ConfirmCancelJobTitle => Pick("确认取消任务", "Confirm Cancel Job");
    public string ConfirmCancelJobButton => Pick("取消任务", "Cancel Job");
    public string ConfirmCancelJobMessage(string sourceFileName, EncodingJobState state) =>
        state switch
        {
            EncodingJobState.Running => IsChinese
                ? $"任务正在编码：{sourceFileName}{Environment.NewLine}{Environment.NewLine}取消后会终止对应编码进程，并使本次任务失败。是否确认取消？"
                : $"The job is currently encoding: {sourceFileName}{Environment.NewLine}{Environment.NewLine}Cancelling it will terminate the encoder process and fail this run. Cancel anyway?",
            EncodingJobState.Queued => IsChinese
                ? $"任务尚未开始：{sourceFileName}{Environment.NewLine}{Environment.NewLine}是否从队列中取消该任务？"
                : $"The job has not started yet: {sourceFileName}{Environment.NewLine}{Environment.NewLine}Remove it from the queue?",
            _ => IsChinese
                ? $"是否取消任务：{sourceFileName}？"
                : $"Cancel job: {sourceFileName}?"
        };
    public string ShuttingDownStatus(int count, bool autoCompressionRunning, bool audioProcessingRunning, bool bluRayDemuxRunning)
    {
        var extraCount = (autoCompressionRunning ? 1 : 0) + (audioProcessingRunning ? 1 : 0) + (bluRayDemuxRunning ? 1 : 0);
        var totalCount = count + extraCount;
        return IsChinese
            ? $"正在终止 {totalCount} 个运行中的任务..."
            : $"Stopping {totalCount} running job(s)...";
    }

    public string Pick(string zh, string en) => IsChinese ? zh : en;

    private string DescribeOppositeSwitchConflict(EncoderArgumentConflict conflict)
    {
        if (conflict.FirstSource == conflict.SecondSource)
        {
            var sourceLabel = GetArgumentSourceLabel(conflict.FirstSource);
            return Pick(
                $"参数冲突：{sourceLabel} 中同时出现 `{conflict.FirstOptionName}` 与 `{conflict.SecondOptionName}`，请只保留一种写法。",
                $"Argument conflict: `{conflict.FirstOptionName}` and `{conflict.SecondOptionName}` both appear in {sourceLabel}. Keep only one form.");
        }

        return Pick(
            $"参数冲突：{GetArgumentSourceLabel(conflict.FirstSource)} 中的 `{conflict.FirstOptionName}` 与 {GetArgumentSourceLabel(conflict.SecondSource)} 中的 `{conflict.SecondOptionName}` 互相冲突，请只保留一种写法。",
            $"Argument conflict: `{conflict.FirstOptionName}` in {GetArgumentSourceLabel(conflict.FirstSource)} conflicts with `{conflict.SecondOptionName}` in {GetArgumentSourceLabel(conflict.SecondSource)}. Keep only one form.");
    }

    private string DescribeConflictingValueConflict(EncoderArgumentConflict conflict)
    {
        var firstValue = conflict.FirstValue ?? string.Empty;
        var secondValue = conflict.SecondValue ?? string.Empty;

        if (conflict.FirstSource == conflict.SecondSource)
        {
            var sourceLabel = GetArgumentSourceLabel(conflict.FirstSource);
            return Pick(
                $"参数冲突：{sourceLabel} 中的 `{conflict.OptionName}` 被设置为不同的值（`{firstValue}` / `{secondValue}`），请只保留一个值。",
                $"Argument conflict: `{conflict.OptionName}` is assigned different values in {sourceLabel} (`{firstValue}` / `{secondValue}`). Keep only one value.");
        }

        return Pick(
            $"参数冲突：`{conflict.OptionName}` 在 {GetArgumentSourceLabel(conflict.FirstSource)} 和 {GetArgumentSourceLabel(conflict.SecondSource)} 中被设置为不同的值（`{firstValue}` / `{secondValue}`），请统一为同一个值。",
            $"Argument conflict: `{conflict.OptionName}` is assigned different values in {GetArgumentSourceLabel(conflict.FirstSource)} and {GetArgumentSourceLabel(conflict.SecondSource)} (`{firstValue}` / `{secondValue}`). Use a single value.");
    }

    private string GetArgumentSourceLabel(EncoderArgumentSource source)
    {
        return source switch
        {
            EncoderArgumentSource.AdditionalArguments => AdditionalArgumentsHeader,
            EncoderArgumentSource.UhdParameters => UhdArgumentsHeader,
            _ => Pick("参数区", "arguments")
        };
    }

    public string ThemeLabel(AppThemePreference preference) =>
        preference switch
        {
            AppThemePreference.Default => Pick("跟随系统", "Use system"),
            AppThemePreference.Light => Pick("浅色", "Light"),
            AppThemePreference.Dark => Pick("深色", "Dark"),
            _ => preference.ToString()
        };

    public string AudioWorkflowLabel(AudioProcessingMode mode) =>
        mode switch
        {
            AudioProcessingMode.Eac3To => "eac3to",
            AudioProcessingMode.Ddp => "deew",
            AudioProcessingMode.Opus => "opus",
            _ => mode.ToString()
        };

    public string BluRayBackendLabel(BluRayDemuxBackend backend) =>
        backend switch
        {
            BluRayDemuxBackend.DgDemux => "DGDemux",
            BluRayDemuxBackend.Eac3To => "eac3to",
            _ => backend.ToString()
        };

    public string BluRayBackendNote(BluRayDemuxBackend backend) =>
        backend switch
        {
            BluRayDemuxBackend.DgDemux => Pick(
                "DGDemux 按播放列表前缀输出，可能额外生成 .qp.txt 等辅助文件。",
                "DGDemux writes files by playlist prefix and may emit auxiliary files such as .qp.txt."),
            BluRayDemuxBackend.Eac3To => Pick(
                "eac3to 按轨道号导出，显式选轨时会自动决定原始扩展名；日志写入程序日志目录。",
                "eac3to exports by track number, auto-resolves raw extensions for explicit selections, and writes logs to the app log folder."),
            _ => string.Empty
        };

    public string AudioEac3ToOutputFormatLabel(AudioEac3ToOutputFormat format) =>
        format switch
        {
            AudioEac3ToOutputFormat.Flac => ".flac",
            AudioEac3ToOutputFormat.Ac3 => ".ac3",
            _ => format.ToString()
        };

    public string AudioChannelProfileLabel(AudioChannelProfile profile) =>
        profile switch
        {
            AudioChannelProfile.Auto => Pick("自动判断", "Auto Detect"),
            AudioChannelProfile.Mono => "1.0",
            AudioChannelProfile.Stereo => "2.0",
            AudioChannelProfile.Surround51 => "5.1",
            AudioChannelProfile.Surround71 => "7.1",
            _ => profile.ToString()
        };

    public string AudioProcessingTelemetrySummary(AudioProcessingTelemetry telemetry)
    {
        var speedText = telemetry.SpeedMultiplier.HasValue && telemetry.SpeedMultiplier.Value > 0
            ? $"{telemetry.SpeedMultiplier.Value:0.#}x"
            : "--";
        var bitrateText = telemetry.BitrateKbps.HasValue && telemetry.BitrateKbps.Value > 0
            ? $"{telemetry.BitrateKbps.Value:0.#} kbps"
            : "--";
        var remainingText = telemetry.Remaining.HasValue
            ? FormatTelemetryDuration(telemetry.Remaining.Value)
            : "--";
        var sizeText = telemetry.EstimatedOutputBytes.HasValue && telemetry.EstimatedOutputBytes.Value > 0
            ? FormatTelemetrySize(telemetry.EstimatedOutputBytes.Value)
            : "--";

        return IsChinese
            ? $"速度 {speedText} · 码率 {bitrateText} · 剩余 {remainingText} · 预估 {sizeText}"
            : $"Speed {speedText} · Bitrate {bitrateText} · ETA {remainingText} · Est. {sizeText}";
    }

    public string UserCaption(string name) => $"{name} · {Pick("用户模板", "User Template")}";

    public string QueueSummary(int running, int queued, int completed, int failed, int cancelled) =>
        IsChinese
            ? $"进行中 {running} · 排队 {queued} · 已完成 {completed} · 失败 {failed} · 已取消 {cancelled}"
            : $"Running {running} · Queued {queued} · Completed {completed} · Failed {failed} · Cancelled {cancelled}";

    public string AudioSourceInfoSummary(string codecName, string channelLabel, int? bitDepth, int? sampleRate, bool isLossless) =>
        IsChinese
            ? $"源信息：{channelLabel} · {codecName} · {(isLossless ? "无损" : "有损/未知")} · {(bitDepth.HasValue ? $"{bitDepth.Value}-bit" : "位深未知")} · {(sampleRate.HasValue ? $"{sampleRate.Value} Hz" : "采样率未知")}"
            : $"Source: {channelLabel} · {codecName} · {(isLossless ? "lossless" : "lossy/unknown")} · {(bitDepth.HasValue ? $"{bitDepth.Value}-bit" : "bit depth unknown")} · {(sampleRate.HasValue ? $"{sampleRate.Value} Hz" : "sample rate unknown")}";

    public string AudioSourceProbeFailed(string message) =>
        IsChinese
            ? $"音频源信息分析失败：{message}"
            : $"Audio source inspection failed: {message}";

    public string AudioSourceRecommendation(AudioSourceInfo? sourceInfo)
    {
        var profile = sourceInfo?.InferProfile();
        if (sourceInfo?.HasStereoOrGreaterChannels() == true && sourceInfo.IsLossless())
        {
            return Pick(
                "当前源已识别为 2.0 及以上无损音频；若你要用 deew，这类输入通常更合适。",
                "This source was identified as lossless 2.0+ audio. If you want to use deew, this kind of input is usually a better fit.");
        }

        return profile switch
        {
            AudioChannelProfile.Mono => Pick("当前源偏向 1.0；你可以按需要手动选择 eac3to、deew 或 opus。", "This source looks mono. Choose eac3to, deew, or opus manually based on your target."),
            AudioChannelProfile.Stereo => Pick("当前源偏向 2.0；如果你准备使用 deew，通常更适合无损输入。", "This source looks stereo. If you plan to use deew, lossless input is usually the better fit."),
            AudioChannelProfile.Surround51 or AudioChannelProfile.Surround71 => Pick("当前源偏向多声道；你可以按需要手动选择 eac3to、deew 或 opus。", "This source looks multichannel. Choose eac3to, deew, or opus manually based on your target."),
            _ => Pick("若未能自动识别声道或编码格式，请手动确认后自行选择工作流。", "If channel or codec detection is uncertain, confirm it manually and choose the workflow yourself.")
        };
    }

    public string AudioCapabilityReadySummary(string workflowLabel) =>
        IsChinese
            ? $"{workflowLabel} 依赖已就绪，可以直接启动。"
            : $"{workflowLabel} dependencies are ready.";

    public string AudioCapabilityUnavailableSummary(string workflowLabel, string detail) =>
        IsChinese
            ? $"{workflowLabel} 依赖未就绪：{detail}"
            : $"{workflowLabel} dependencies are not ready: {detail}";

    public string BluRayToolPreparing => Pick("正在准备蓝光解复用工具状态。", "Preparing Blu-ray demux tool readiness.");

    public string BluRayToolReadySummary(string backendLabel, string detail) =>
        IsChinese
            ? $"{backendLabel} 已就绪：{detail}"
            : $"{backendLabel} is ready: {detail}";

    public string BluRayToolUnavailableSummary(string backendLabel, string detail) =>
        IsChinese
            ? $"{backendLabel} 未就绪：{detail}"
            : $"{backendLabel} is not ready: {detail}";

    public string BluRayTrackSelectionSummary(int selected, int total) =>
        IsChinese
            ? $"已选 {selected} / {total} 条轨道"
            : $"{selected} / {total} tracks selected";

    public string ReadinessStateLabel(ReadinessState state) =>
        state switch
        {
            ReadinessState.Ready => Pick("已就绪", "Ready"),
            ReadinessState.Missing => Pick("缺失", "Missing"),
            ReadinessState.Misconfigured => Pick("异常", "Misconfigured"),
            ReadinessState.Partial => Pick("部分可用", "Partial"),
            ReadinessState.Unknown => Pick("未知", "Unknown"),
            _ => state.ToString()
        };

    public string ToolMissingDetail(string toolLabel) =>
        IsChinese
            ? $"未找到 {toolLabel}。"
            : $"{toolLabel} was not found.";

    public string ToolUnknownDetail(string toolLabel) =>
        IsChinese
            ? $"暂时无法判断 {toolLabel} 的状态。"
            : $"The status of {toolLabel} could not be determined.";

    public string ToolDetectionSourceLabel(ToolDetectionSource source, string sourceLabel) =>
        source switch
        {
            ToolDetectionSource.ManualSelection => $"{Pick("手动固定", "Manual Pin")} · {sourceLabel}",
            ToolDetectionSource.LocalToolset => $"{Pick("本地工具链", "Local Toolset")} · {sourceLabel}",
            ToolDetectionSource.LocalTools => $"{Pick("本地工具目录", "Local Tools")} · {sourceLabel}",
            ToolDetectionSource.EnvironmentVariable => $"{Pick("环境变量", "Environment Variable")} · {sourceLabel}",
            ToolDetectionSource.Path => $"PATH · {sourceLabel}",
            ToolDetectionSource.SystemEncoder => $"{Pick("系统编码器", "System Encoder")} · {sourceLabel}",
            ToolDetectionSource.SpecialLocation => $"{Pick("特殊位置", "Special Location")} · {sourceLabel}",
            _ => sourceLabel
        };

    public string RefreshCompletedStatus(DateTime time) =>
        IsChinese
            ? $"工具链已刷新，{time:HH:mm:ss}"
            : $"Toolchain refreshed at {time:HH:mm:ss}.";

    public string RefreshFailedStatus(string message) =>
        IsChinese
            ? $"刷新失败：{message}"
            : $"Refresh failed: {message}";

    public string NoUpdatesFoundStatus => Pick("已完成远程检查，当前没有匹配规则的编码器更新。", "Remote check finished. No matching encoder updates were found.");

    public string UpdatesFoundStatus(int count) =>
        IsChinese
            ? $"已发现 {count} 个可自动安装的编码器更新。"
            : $"Found {count} encoder updates that can be installed automatically.";

    public string NoSetupDependencyUpdatesStatus =>
        Pick(
            "已完成远程检查，当前没有需要更新的首启依赖。",
            "Remote check finished. No setup dependencies need an update.");

    public string SetupDependencyUpdatesFoundStatus(int count) =>
        IsChinese
            ? $"已发现 {count} 项首启依赖可更新。"
            : $"Found {count} setup dependencies with updates.";

    public string UpdatesCheckFailedStatus(string message) =>
        IsChinese
            ? $"检查更新失败：{message}"
            : $"Update check failed: {message}";

    public string AppUpdateAvailableStatus(string currentVersion, string latestVersion) =>
        IsChinese
            ? $"检测到新版本：{latestVersion}（当前版本号 {currentVersion}）。"
            : $"A newer app release is available: {latestVersion} (current: {currentVersion}).";

    public string AppUpdateDownloadingStatus(string latestVersion, int? progressPercent = null) =>
        IsChinese
            ? progressPercent.HasValue
                ? $"正在下载 {latestVersion} 的安装包，进度 {progressPercent.Value}%。"
                : $"正在下载 {latestVersion} 的安装包。"
            : progressPercent.HasValue
                ? $"Downloading the installer for {latestVersion}: {progressPercent.Value}%."
                : $"Downloading the installer for {latestVersion}.";

    public string AppUpdateManualDownloadStatus(string currentVersion, string latestVersion) =>
        IsChinese
            ? $"检测到新版本：{latestVersion}（当前版本号 {currentVersion}），但当前发布未提供可校验安装包，请改为打开发布页手动更新。"
            : $"A newer app release is available: {latestVersion} (current: {currentVersion}), but no verifiable installer was published. Open the release page for a manual update.";

    public string AppUpdateInstallerReadyStatus =>
        Pick(
            "更新安装包已下载完成，可以立即安装。",
            "The update installer has finished downloading and is ready to install.");

    public string AppUpdateDownloadFailedStatus(string message) =>
        IsChinese
            ? $"下载安装包失败：{message}"
            : $"Failed to download the installer: {message}";

    public string AppUpdateReadyTitle => Pick("安装更新", "Install Update");

    public string AppUpdateReadyMessage =>
        Pick(
            "更新安装包已下载完成。是否立即关闭程序并启动安装器？",
            "The update installer has finished downloading. Close the app and launch the installer now?");

    public string AppUpdateInstallRequiresIdleMessage =>
        Pick(
            "当前仍有进行中的解复用、压制或转码任务。请先停止这些任务，再执行安装更新。",
            "Demux, encode, or transcode work is still running. Stop those tasks before installing the update.");

    public string InstallNowButton => Pick("立即安装", "Install Now");
    public string LaterButton => Pick("稍后", "Later");

    public string AppReleaseNotPublishedStatus(string currentVersion) =>
        IsChinese
            ? $"未检测到更新，当前版本号 {currentVersion}。"
            : $"No stable app release has been published yet. Current version: {currentVersion}.";

    public string AppUpdateIdleStatus =>
        Pick(
            "尚未检查主程序更新。",
            "App updates have not been checked yet.");

    public string AppAlreadyLatestStatus(string currentVersion) =>
        IsChinese
            ? $"未检测到更新，当前版本号 {currentVersion}。"
            : $"The app is already on the latest stable release: {currentVersion}.";

    public string AppCurrentVersionAheadStatus(string currentVersion, string latestVersion) =>
        IsChinese
            ? $"未检测到更新，当前版本号 {currentVersion}。"
            : $"The current app version {currentVersion} is newer than the latest stable release {latestVersion}.";

    public string AppUpdateComparisonUnavailableStatus(string currentVersion, string latestVersion) =>
        IsChinese
            ? $"未检测到更新，当前版本号 {currentVersion}。"
            : $"App update check finished. Current version: {currentVersion}; latest release: {latestVersion}.";

    public string AppCurrentVersionLabel(string currentVersion) =>
        IsChinese
            ? $"当前版本：{currentVersion}"
            : $"Current version: {currentVersion}";

    public string AppLatestVersionLabel(string latestVersion) =>
        IsChinese
            ? $"发布页稳定版：{latestVersion}"
            : $"Latest stable release: {latestVersion}";

    public string AutoCompressionStartingStatus(string sourceFileName) =>
        IsChinese ? $"开始自动压制：{sourceFileName}" : $"Auto encode started: {sourceFileName}";

    public string AudioProcessingStartingStatus(string sourceFileName, string workflowLabel) =>
        IsChinese ? $"开始 {workflowLabel}：{sourceFileName}" : $"{workflowLabel} started: {sourceFileName}";

    public string AudioProcessingCompletedStatus(string workflowLabel) =>
        IsChinese ? $"{workflowLabel} 已完成。" : $"{workflowLabel} completed.";

    public string AudioProcessingCancelledStatus(string workflowLabel) =>
        IsChinese ? $"{workflowLabel} 已取消。" : $"{workflowLabel} cancelled.";

    public string AudioProcessingCancellingStatus(string workflowLabel) =>
        IsChinese ? $"正在取消 {workflowLabel}..." : $"Cancelling {workflowLabel}...";

    public string AudioProcessingFailedStatus(string detail) =>
        IsChinese ? $"音频处理失败：{detail}" : $"Audio processing failed: {detail}";

    public string BluRayDiscScanStatus(string backendLabel) =>
        IsChinese ? $"正在使用 {backendLabel} 扫描蓝光目录..." : $"Scanning the Blu-ray folder with {backendLabel}...";

    public string BluRayDiscScanCompletedStatus(string backendLabel, int count) =>
        IsChinese ? $"{backendLabel} 扫描完成，共发现 {count} 条播放列表。" : $"{backendLabel} scan completed. Found {count} playlists.";

    public string BluRayDiscScanFailedStatus(string message) =>
        IsChinese ? $"蓝光扫描失败：{message}" : $"Blu-ray scan failed: {message}";

    public string BluRayPlaylistLoadStatus(string playlistName) =>
        IsChinese ? $"正在加载播放列表：{playlistName}" : $"Loading playlist: {playlistName}";

    public string BluRayPlaylistLoadedStatus(string playlistName, int trackCount) =>
        IsChinese ? $"播放列表已加载：{playlistName} · {trackCount} 条轨道" : $"Playlist loaded: {playlistName} · {trackCount} tracks";

    public string BluRayPlaylistLoadFailedStatus(string message) =>
        IsChinese ? $"播放列表加载失败：{message}" : $"Playlist load failed: {message}";

    public string BluRayDemuxStartingStatus(string backendLabel, string playlistName) =>
        IsChinese ? $"{backendLabel} 开始解复用：{playlistName}" : $"{backendLabel} demux started: {playlistName}";

    public string BluRayDemuxAnalyzingStatus(string backendLabel, double? percent = null)
    {
        if (!percent.HasValue)
        {
            return IsChinese ? $"{backendLabel} 分析中..." : $"{backendLabel} analyzing...";
        }

        return IsChinese
            ? $"{backendLabel} 分析中 {percent.Value:0.#}%"
            : $"{backendLabel} analyzing {percent.Value:0.#}%";
    }

    public string BluRayDemuxCompletedStatus(string backendLabel) =>
        IsChinese ? $"{backendLabel} 解复用完成。" : $"{backendLabel} demux completed.";

    public string BluRayDemuxCancelledStatus(string backendLabel) =>
        IsChinese ? $"{backendLabel} 解复用已取消。" : $"{backendLabel} demux cancelled.";

    public string BluRayDemuxCancellingStatus(string backendLabel) =>
        IsChinese ? $"正在取消 {backendLabel} 解复用..." : $"Cancelling {backendLabel} demux...";

    public string BluRayDemuxFailedStatus(string detail) =>
        IsChinese ? $"蓝光解复用失败：{detail}" : $"Blu-ray demux failed: {detail}";

    public string BluRayTaskClearedStatus => Pick("蓝光解复用任务已清空。", "Blu-ray demux task cleared.");

    public string AutoCompressionCancellingStatus => Pick("正在取消自动压制任务...", "Cancelling auto encode...");

    public string AutoCompressionCompletedStatus => Pick("自动压制完成。", "Auto encode completed.");

    public string AutoCompressionCancelledStatus => Pick("自动压制已取消。", "Auto encode cancelled.");

    public string AutoCompressionFailedStatus(string message) =>
        IsChinese ? $"自动压制失败：{message}" : $"Auto encode failed: {message}";

    public string TemplateSavedStatus(string name) =>
        IsChinese
            ? $"模板已保存：{name}"
            : $"Template saved: {name}";

    public string TemplateImportedStatus(string name) =>
        IsChinese
            ? $"模板已导入：{name}"
            : $"Template imported: {name}";

    public string TemplateExportedStatus(string filePath) =>
        IsChinese
            ? $"模板已导出：{filePath}"
            : $"Template exported: {filePath}";

    public string TemplatePinnedStatus(string name) =>
        IsChinese
            ? $"模板已置顶：{name}"
            : $"Template pinned: {name}";

    public string TemplateUnpinnedStatus(string name) =>
        IsChinese
            ? $"模板已取消置顶：{name}"
            : $"Template unpinned: {name}";

    public string InstallAlreadyRunning => Pick("当前已有依赖安装任务在运行。", "Another setup dependency operation is already running.");
    public string SetupDependencyImportedStatus(string dependencyLabel) =>
        IsChinese
            ? $"{dependencyLabel} 已导入到工作目录。"
            : $"{dependencyLabel} was imported into the workspace folder.";

    public string SetupDependencyUninstalledStatus(string dependencyLabel) =>
        IsChinese
            ? $"{dependencyLabel} 已卸载。"
            : $"{dependencyLabel} was uninstalled.";

    public string SetupDependencyExternalLocationWarning =>
        Pick(
            "当前检测结果来自工作目录外部。更新会写入工作目录，但卸载只会处理程序自己托管的副本。",
            "The detected binary lives outside the workspace folder. Update writes a managed copy into the workspace folder, while uninstall only removes the app-managed copy.");

    public string JobQueuedStatus(string sourceFileName, bool startImmediately, bool hasRunningJob)
    {
        if (IsChinese)
        {
            if (!startImmediately)
            {
                return hasRunningJob
                    ? $"作业已加入队列：{sourceFileName}"
                    : $"作业已加入队列，等待开始：{sourceFileName}";
            }

            return hasRunningJob
                ? $"作业已加入队列：{sourceFileName}"
                : $"作业已加入队列并开始：{sourceFileName}";
        }

        if (!startImmediately)
        {
            return hasRunningJob
                ? $"Queued: {sourceFileName}"
                : $"Queued and waiting: {sourceFileName}";
        }

        return hasRunningJob
            ? $"Queued: {sourceFileName}"
            : $"Queued and started: {sourceFileName}";
    }

    public string QueuedJobCancelledStatus(string sourceFileName) =>
        IsChinese ? $"已取消排队作业：{sourceFileName}" : $"Queued job cancelled: {sourceFileName}";

    public string RunningJobCancellingStatus(string sourceFileName) =>
        IsChinese ? $"正在取消作业：{sourceFileName}" : $"Cancelling job: {sourceFileName}";

    public string JobRestartedStatus(string sourceFileName) =>
        IsChinese ? $"任务已重新加入队列：{sourceFileName}" : $"Job re-queued: {sourceFileName}";

    public string RemoveJobMissingError => Pick("未找到要删除的任务。", "The job to delete was not found.");
    public string RemoveRunningJobError => Pick("运行中的任务请先取消，再删除。", "Cancel a running job before deleting it.");
    public string RemoveJobFailedError => Pick("任务删除失败。", "Failed to delete the job.");

    public string JobDeletedStatus(string sourceFileName) =>
        IsChinese ? $"任务已删除：{sourceFileName}" : $"Job deleted: {sourceFileName}";

    public string StartJobMissingError => Pick("未找到要开始的任务。", "The job to start was not found.");
    public string StartJobInvalidError => Pick("只有排队中的任务才能开始。", "Only queued jobs can be started.");
    public string ConcurrentEncodingLimitReached(int limit) =>
        IsChinese
            ? $"当前同时编码任务数已达到上限：{limit}。"
            : $"The concurrent encode job limit has been reached: {limit}.";

    public string JobStartedManuallyStatus(string sourceFileName) =>
        IsChinese ? $"已手动开始：{sourceFileName}" : $"Started manually: {sourceFileName}";

    public string EncodingStartedStatus(string sourceFileName) =>
        IsChinese ? $"开始编码：{sourceFileName}" : $"Encoding started: {sourceFileName}";

    public string EncodingFinishedStatus(string sourceFileName, string summary) =>
        IsChinese ? $"{sourceFileName}：{summary}" : $"{sourceFileName}: {summary}";

    public string EncodingCancelledStatus(string sourceFileName) =>
        IsChinese ? $"已取消：{sourceFileName}" : $"Cancelled: {sourceFileName}";

    public string EncodingFailedStatus(string sourceFileName) =>
        IsChinese ? $"失败：{sourceFileName}" : $"Failed: {sourceFileName}";

    public string MoveJobMissingError => Pick("未找到要调整顺序的任务。", "The job to reorder was not found.");
    public string MoveJobInvalidError => Pick("只有排队中的任务才能调整顺序。", "Only queued jobs can be reordered.");
    public string MoveJobNotInQueueError => Pick("任务不在当前队列中。", "The job is not in the current queue.");

    public string MoveJobEdgeStatus(MoveQueuedJobMode mode, string fileName)
    {
        if (IsChinese)
        {
            return mode switch
            {
                MoveQueuedJobMode.Next => $"任务已经是下一项：{fileName}",
                MoveQueuedJobMode.Top => $"任务已经位于可执行区顶部：{fileName}",
                MoveQueuedJobMode.Bottom => $"任务已经位于队尾：{fileName}",
                MoveQueuedJobMode.Up => $"任务已经无法继续上移：{fileName}",
                MoveQueuedJobMode.Down => $"任务已经无法继续下移：{fileName}",
                _ => fileName
            };
        }

        return mode switch
        {
            MoveQueuedJobMode.Next => $"Already next: {fileName}",
            MoveQueuedJobMode.Top => $"Already at the top: {fileName}",
            MoveQueuedJobMode.Bottom => $"Already at the bottom: {fileName}",
            MoveQueuedJobMode.Up => $"Cannot move up: {fileName}",
            MoveQueuedJobMode.Down => $"Cannot move down: {fileName}",
            _ => fileName
        };
    }

    public string MoveJobCompletedStatus(MoveQueuedJobMode mode, string fileName)
    {
        if (IsChinese)
        {
            return mode switch
            {
                MoveQueuedJobMode.Next => $"已设为下一项：{fileName}",
                MoveQueuedJobMode.Top => $"已移到可执行区顶部：{fileName}",
                MoveQueuedJobMode.Bottom => $"已移到队尾：{fileName}",
                MoveQueuedJobMode.Up => $"已上移任务：{fileName}",
                MoveQueuedJobMode.Down => $"已下移任务：{fileName}",
                _ => $"已调整任务顺序：{fileName}"
            };
        }

        return mode switch
        {
            MoveQueuedJobMode.Next => $"Set as next: {fileName}",
            MoveQueuedJobMode.Top => $"Moved to top: {fileName}",
            MoveQueuedJobMode.Bottom => $"Moved to bottom: {fileName}",
            MoveQueuedJobMode.Up => $"Moved up: {fileName}",
            MoveQueuedJobMode.Down => $"Moved down: {fileName}",
            _ => $"Reordered: {fileName}"
        };
    }

    public string MissingEncoderError => Pick("请先选择编码器，并完成当前草稿的关键参数。", "Select an encoder and finish the key draft parameters first.");
    public string MissingSourceError => Pick("请先选择源文件。", "Select a source file first.");
    public string MissingOutputError => Pick("请先指定输出目录。", "Select an output directory first.");
    public string OutputDirectoryInvalidError => Pick("输出目录不能指向现有文件。请改为选择目录。", "The output directory cannot point to an existing file. Choose a folder instead.");
    public string SourceFileMissingError => Pick("未找到源文件。", "The source file was not found.");
    public string SourceOutputPathConflictError => Pick("输出文件不能与源文件相同。请更换输出目录或输出格式。", "The output file must be different from the source. Choose another output folder or output format.");
    public string AutoCompressionMissingEncoderError => Pick("请先选择自动压制编码器。", "Select an auto-encode encoder first.");
    public string AutoCompressionMissingSourceError => Pick("请先选择自动压制输入源。", "Select an auto-encode source first.");
    public string AutoCompressionMissingOutputError => Pick("请先指定自动压制输出目录。", "Select an auto-encode output directory first.");
    public string AutoCompressionOutputDirectoryInvalidError => Pick("自动压制输出目录不能指向现有文件。请改为选择目录。", "The auto-encode output directory cannot point to an existing file. Choose a folder instead.");
    public string AutoCompressionSourceFileMissingError => Pick("自动压制输入源不存在。", "The auto-encode source file was not found.");
    public string AutoCompressionSourceOutputPathConflictError => Pick("自动压制输出文件不能与输入源相同。请更换输出目录。", "The auto-encode output file must be different from the source. Choose another output folder.");
    public string AutoCompressionAlreadyRunningError => Pick("当前已有自动压制任务在运行。", "An auto-encode task is already running.");
    public string AudioSourceMissingError => Pick("请先选择音频输入源。", "Select an audio source first.");
    public string AudioOutputMissingError => Pick("请先指定音频输出目录。", "Select an audio output directory first.");
    public string AudioOutputDirectoryInvalidError => Pick("音频输出目录不能指向现有文件。请改为选择目录。", "The audio output directory cannot point to an existing file. Choose a folder instead.");
    public string AudioSourceOutputPathConflictError => Pick("音频输出文件不能与输入源相同。请更换输出目录或目标格式。", "The audio output file must be different from the source. Choose another output folder or target format.");
    public string AudioProcessingAlreadyRunningError => Pick("当前已有音频处理任务在运行。", "An audio task is already running.");
    public string BluRayDiscSourceMissingError => Pick("请先选择蓝光目录。", "Select a Blu-ray folder first.");
    public string BluRayDiscStructureInvalidError => Pick("输入目录中未找到有效的 BDMV/PLAYLIST 结构。", "The selected folder does not contain a valid BDMV/PLAYLIST structure.");
    public string BluRayOutputDirectoryMissingError => Pick("请先指定解复用输出目录。", "Select a demux output directory first.");
    public string BluRayOutputDirectoryInvalidError => Pick("解复用输出目录不能指向现有文件。请改为选择目录。", "The demux output directory cannot point to an existing file. Choose a folder instead.");
    public string BluRayPlaylistMissingError => Pick("请先选择一条播放列表。", "Select a playlist first.");
    public string BluRayTrackSelectionMissingError => Pick("请至少选择一条需要导出的轨道。", "Select at least one track to export.");
    public string BluRayDemuxAlreadyRunningError => Pick("当前已有蓝光解复用任务在运行。", "A Blu-ray demux task is already running.");
    public string BluRayToolMissingError(string backendLabel) =>
        IsChinese
            ? $"{backendLabel} 未就绪，请先检查当前环境。"
            : $"{backendLabel} is not ready. Check the current environment first.";
    public string AudioWorkflowCapabilityMissingError(string workflowLabel) =>
        IsChinese
            ? $"{workflowLabel} 依赖未就绪，请先检查环境页中的音频能力。"
            : $"{workflowLabel} dependencies are not ready. Check the environment section first.";
    public string AudioWorkflowSourceMismatchError(string workflowLabel, string allowedProfiles) =>
        IsChinese
            ? $"{workflowLabel} 当前只支持 {allowedProfiles}。如未识别到声道，请手动指定。"
            : $"{workflowLabel} currently supports only {allowedProfiles}. Choose the channel layout manually if detection is unavailable.";
    public string AudioDirectSourceUnsupportedError(string workflowLabel) =>
        IsChinese
            ? $"{workflowLabel} 当前只接入直接音频源或单音轨容器，暂不处理 .mkv / .mp4 / .m2ts 这类多音轨视频容器。"
            : $"{workflowLabel} currently supports direct audio sources or single-audio containers only, not general multi-track video containers such as .mkv / .mp4 / .m2ts.";
    public string AudioEac3ToOutputFormatMissingError => Pick("请先选择 eac3to 的目标格式。", "Select an eac3to target format first.");
    public string AudioEac3ToArgumentsInvalidError(string invalidArguments) =>
        IsChinese
            ? $"eac3to 额外参数只允许 -down16 和 -mono。无效参数：{invalidArguments}"
            : $"eac3to extra arguments only allow -down16 and -mono. Invalid arguments: {invalidArguments}";
    public string AudioOpusBitrateMissingError => Pick("请先选择 Opus 码率。", "Select an Opus bitrate first.");
    public string AudioOpusBitrateLabel(int bitrateKbps) =>
        bitrateKbps switch
        {
            510 => Pick("7.1声道建议 510 kbps", "Recommended for 7.1: 510 kbps"),
            384 => Pick("5.1声道建议 384 kbps", "Recommended for 5.1: 384 kbps"),
            192 => Pick("2.0声道建议 192 kbps", "Recommended for 2.0: 192 kbps"),
            96 => Pick("1.0声道建议 96 kbps", "Recommended for 1.0: 96 kbps"),
            _ => $"{bitrateKbps} kbps"
        };

    public string ActualCommandTitle(string profileName) =>
        IsChinese ? $"{profileName} · 实际执行命令" : $"{profileName} · Actual Command";

    public string ActualDraftNotReadyMessage(string message) =>
        IsChinese
            ? $"实际作业草稿未就绪：{message}"
            : $"The actual job draft is not ready: {message}";

    public string ResolvedBinaryMissing => Pick("当前尚未发现可用编码器。", "No available encoder was found.");

    public string ResolvedBinarySummary(string sourceSummary, string executableName) =>
        IsChinese
            ? $"将调用 {sourceSummary} 中的 {executableName}。"
            : $"Will use {executableName} from {sourceSummary}.";

    public string ResolvedPreviewNotes(string outputPath, string binarySummary) =>
        IsChinese
            ? $"当前草稿会使用自动识别输入模式，默认按 x64 优先匹配编码器，输出写入 {outputPath}。{Environment.NewLine}{binarySummary}"
            : $"The current draft uses automatic input detection, prefers x64 encoders by default, and writes output to {outputPath}.{Environment.NewLine}{binarySummary}";

    public string BinarySourceType(EncoderBinarySource source) =>
        source switch
        {
            EncoderBinarySource.ManualSelection => Pick("手动固定", "Manual Pin"),
            EncoderBinarySource.EnvironmentVariable => Pick("环境变量", "Environment Variable"),
            EncoderBinarySource.Path => "PATH",
            EncoderBinarySource.LocalToolset => Pick("本地工具链", "Local Toolset"),
            _ => source.ToString()
        };

    public string BinarySourceSummary(EncoderBinarySource source, string sourceLabel) => $"{BinarySourceType(source)} · {sourceLabel}";

    public string QueueSelectionSummary(string stateLabel, string summary) => $"{stateLabel} · {summary}";

    public string OutputFileTypeLabel(string extension) =>
        IsChinese ? $"编码输出 ({extension})" : $"Encoded Output ({extension})";

    public string PipelinePreviewTitle(string profileName, EncoderKind kind) =>
        kind switch
        {
            EncoderKind.X264 => IsChinese ? $"{profileName} · x264 管线预览" : $"{profileName} · x264 Pipeline Preview",
            EncoderKind.X265 => IsChinese ? $"{profileName} · x265 管线预览" : $"{profileName} · x265 Pipeline Preview",
            EncoderKind.SvtAv1 => IsChinese ? $"{profileName} · SVT-AV1 管线预览" : $"{profileName} · SVT-AV1 Pipeline Preview",
            _ => profileName
        };

    public string PipelinePreviewNotes(EncoderKind kind) =>
        kind switch
        {
            EncoderKind.X264 => Pick(
                "命令预览使用 VapourSynth 管线占位符。实际加入队列后，会自动识别 .avs / .vpy / .mkv / .mp4 / .avi / .flv / .y4m / .yuv。",
                "The preview uses a VapourSynth placeholder. Real jobs auto-detect .avs / .vpy / .mkv / .mp4 / .avi / .flv / .y4m / .yuv."),
            EncoderKind.X265 => Pick(
                "x265 使用 y4m 管道输入模型。加入队列后，会自动对 .mkv / .mp4 / .avi / .flv 走 FFmpeg 解码管线，.yuv 则按 raw 输入处理。",
                "x265 uses a y4m pipe input model. Queued jobs route .mkv / .mp4 / .avi / .flv through FFmpeg, while .yuv is treated as raw input."),
            EncoderKind.SvtAv1 => Pick(
                "当前默认输出 IVF。加入队列后，会自动识别 .avs / .vpy / .mkv / .mp4 / .avi / .flv / .y4m / .yuv。",
                "The default output is IVF. Queued jobs auto-detect .avs / .vpy / .mkv / .mp4 / .avi / .flv / .y4m / .yuv."),
            _ => string.Empty
        };

    public string ErrorCannotQueueTitle => Pick("无法加入队列", "Unable to queue");
    public string ErrorCannotStartAutoCompressionTitle => Pick("无法启动自动压制", "Unable to start auto encode");
    public string ErrorCannotStartAudioProcessingTitle => Pick("无法启动音频处理", "Unable to start audio processing");
    public string ErrorCannotStartBluRayDemuxTitle => Pick("无法启动蓝光解复用", "Unable to start Blu-ray demux");
    public string ErrorCannotReorderTitle => Pick("无法调整任务顺序", "Unable to reorder");
    public string ErrorCannotStartTitle => Pick("无法开始任务", "Unable to start");
    public string ErrorCannotRestartTitle => Pick("无法重启任务", "Unable to restart");

    private static string FormatTelemetryDuration(TimeSpan duration)
    {
        duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private static string FormatTelemetrySize(long bytes)
    {
        const double kilo = 1024;
        const double mega = kilo * 1024;
        const double giga = mega * 1024;

        var value = (double)bytes;
        return value switch
        {
            >= giga => $"{value / giga:0.##} GB",
            >= mega => $"{value / mega:0.##} MB",
            >= kilo => $"{value / kilo:0.##} KB",
            _ => $"{bytes} B"
        };
    }
    public string ErrorCannotDeleteTitle => Pick("无法删除任务", "Unable to delete");
    public string ErrorSelectionFailedTitle => Pick("选择失败", "Selection Failed");
    public string ErrorInstallFailedTitle => Pick("安装失败", "Install Failed");
    public string ErrorUninstallFailedTitle => Pick("卸载失败", "Uninstall Failed");
    public string ErrorSaveFailedTitle => Pick("保存失败", "Save Failed");
    public string ErrorImportFailedTitle => Pick("导入失败", "Import Failed");
    public string ErrorExportFailedTitle => Pick("导出失败", "Export Failed");
    public string ErrorDeleteFailedTitle => Pick("删除失败", "Delete Failed");
    public string ErrorPinFailedTitle => Pick("置顶操作失败", "Pin Action Failed");
    public string ErrorSaveSettingsFailedTitle => Pick("保存设置失败", "Save Settings Failed");
    public string ErrorCannotSaveTemplateTitle => Pick("无法保存模板", "Unable to Save Template");
    public string EmptyTemplateNameMessage => Pick("模板名称不能为空。", "Template name cannot be empty.");
    public string IncompleteTemplateMessage => Pick("请先选择一个预设，并填写模板名称。", "Select a preset and provide a template name first.");
    public string TemplateMissingMessage => Pick("模板不存在或已被移除。", "The template no longer exists.");
    public string PinnedTemplateLockedMessage => Pick(
        "置顶模板需要先取消置顶，之后才能修改或删除。",
        "Pinned templates must be unpinned before they can be modified or deleted.");
    public string ConfirmDeleteTemplateTitle => Pick("删除模板", "Delete Template");
    public string ConfirmDeleteTemplateMessage(string name) =>
        IsChinese ? $"确认删除模板“{name}”？此操作无法撤销。" : $"Delete the template \"{name}\"? This cannot be undone.";
    public string OverwriteTemplateTitle => Pick("覆盖模板", "Overwrite Template");
    public string OverwriteTemplateMessage(string name) =>
        IsChinese ? $"已存在同名模板“{name}”，是否覆盖？" : $"A template named \"{name}\" already exists. Overwrite it?";
    public string UnsavedTemplateChangesTitle => Pick("保存模板修改", "Save Template Changes");
    public string UnsavedTemplateChangesMessage => Pick("当前模板有未保存的修改，是否先保存？", "The current template has unsaved changes. Save first?");
}
