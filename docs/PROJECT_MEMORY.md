# FlowEncode Project Memory

本文件记录需要长期稳定执行的项目级约定。除非用户明确要求变更，否则这里的规则应视为默认真相来源。

## Release Memory

- 每次正式发版默认只提供单资产：
  - `FlowEncode-Setup.exe`
- 资产名不再附加版本号、`win-x64` 或其他 RID 后缀。版本信息只体现在 Git tag、GitHub Release 标题和更新说明中。
- GitHub Release 标题固定为 `FlowEncode <version>`，tag 固定为 `v<version>`。
- 正式资产只面向 Windows x64。
- 正式资产必须是干净布局，不得包含运行时生成内容或开发机本地状态，例如：
  - `data`
  - `downloads`
  - `encoders`
  - `tools`
  - `toolset`
  - 调试符号、缓存、日志、临时文件
- 安装版使用 Inno Setup 构建，脚本位置固定为 [FlowEncode.iss](/D:/codex/FlowEncode/installer/FlowEncode.iss)。
- 资产构建统一使用 [build-release-assets.ps1](/D:/codex/FlowEncode/scripts/build-release-assets.ps1)。

## Release Notes Memory

- 更新说明默认使用中文。
- 默认结构保持精简，按以下顺序书写：
  - 标题
  - 简介段
  - `### Highlights`
  - `### Upgrade Notes`
- `Highlights` 只写用户可感知的重要变化，不写开发过程。
- `Upgrade Notes` 必须至少说明：
  - Windows x64 only
  - 仅提供 `FlowEncode-Setup.exe`
  - 是否存在缓存迁移、配置迁移、发布形态变更或兼容性注意事项
- 只有在功能量级明显较大时，才允许在 `Highlights` 和 `Upgrade Notes` 之间额外增加模块章节；如果增加，也必须保持简洁，不写流水账。

## README Memory

- `README.md` 默认使用中文。
- `README.md` 的定位是产品级说明文档，不是开发日志、路线图或变更流水账。
- `README.md` 开头必须明确：
  - 项目是面向 Windows x64 的桌面端工作流前端
  - 项目定位是工作流编排与环境管理前端，而不是把全部运行时、插件和第三方工具静态打包在一起的一体化整合包
- 若无明确需要，`README.md` 默认保持当前主结构与顺序：
  - 标题
  - 简介段
  - 顶部强调说明
  - `## 项目概览`
  - `## 核心能力`
  - `## 支持的工具链`
  - `## 发行与依赖`
  - `## 本地数据与隐私`
  - `## 获取与反馈`
  - `## 许可证`
- `核心能力` 应优先描述用户可感知模块与工作流，不写内部实现细节。
- `支持的工具链` 应只描述当前真实支持的编码器、脚本运行时和外部工具，不提前宣传未落地能力。
- `发行与依赖` 必须至少说明：
  - Windows x64 only
  - 仅提供安装版资产 `FlowEncode-Setup.exe`
  - Python、VapourSynth 及其插件生态是否仍按上游方式独立安装与维护
- `本地数据与隐私` 必须至少说明本地目录写入位置、日志/缓存可能包含的敏感信息类型，以及分享前建议脱敏。
- 如果发生以下变化，相关改动应在同一次交付中同步更新 `README.md`：
  - 产品定位变化
  - 核心工作流增删
  - 支持工具链变化
  - 发布形态变化
  - 本地数据目录或隐私说明变化
- `README.md` 默认避免写死仅对某一版本成立的临时说明；若必须写版本相关内容，应确保与当前仓库状态一致。

## Republishing Memory

- 如果某个版本已经创建了错误的 GitHub Release，需要先删除现有 release 和远端 tag，再基于当前正确提交重新打 tag 和重发。
- 重发时仍然沿用原版本号，除非用户明确要求改版本号。

## Upstream Sync Memory

- 后续默认以 FlowEncode 作为主要维护线。
- 每次修改 FlowEncode 时，都要评估是否存在可同步回 CMCT 原项目的通用改动。
- 应同步回 [CMCT_Encode](/D:/codex/CMCT_Encode) 的改动包括：
  - 功能 bug 修复
  - 交互行为修复
  - 性能、稳定性、线程/异步、资源释放修复
  - 测试补强
  - 与品牌无关的构建、发布脚本健壮性改进
- 不应同步回 CMCT 的 FlowEncode 专属改动包括：
  - 产品名、命名空间、程序集名、进程名、安装器名
  - 图标、banner、logo、品牌视觉资产
  - GitHub 仓库地址、自动更新源、发布资产名
  - AppId、AUMID、manifest identity、LocalAppData 目录名
  - README、Release Notes 等只描述 FlowEncode 的文本
- 同步方式必须以“等价功能补丁”为准，不能把 FlowEncode 文件整仓覆盖到 CMCT；涉及命名空间或路径时需要保留 CMCT 原有命名。
- 如果某个改动是否应同步不明确，应在交付说明中明确列为“未同步原因/待确认”，不要静默跳过。

## VapourSynth Workspace Memory

- 当前已确认要做一个独立的 VapourSynth 子项目，目标范围固定为：
  - 完整脚本编辑器
  - 完整 `Preview / F5` 视频预览交互
- 该子项目明确排除以下范围：
  - 音频预览
  - 音频缓存
  - 音量或音频设备选择
  - `Encode / Jobs / Watcher / Benchmark`
  - 模板系统、热键编辑器、主题编辑器、VapourSynth 路径编辑器
- UI 形态固定为：
  - 主体程序内提供一个独立的 VapourSynth 编辑器页面
  - 预览使用独立弹出窗口，不嵌入主页面
- 技术路线固定为：
  - 主体 UI 继续使用现有 `C# + WinUI 3`
  - 编辑器使用 `WebView2` 宿主网页代码编辑器
  - 预览核心使用独立原生 helper 负责 VapourSynth 脚本求值、视频输出枚举、视频帧抓取与帧属性查询
- 该子项目的默认开发顺序固定为四步，后续若无用户明确变更，不应跳步或改序：
  - 第一步：开发编辑器
  - 第二步：开发预览界面
  - 第三步：把这两个功能整合进主体程序
  - 第四步：审查、debug、release

## 视觉系统重构 Memory

- “视觉系统重构”默认指对现有 WinUI 3 界面做 Fluent Design 视觉系统重构，而不是业务、导航、绑定或架构重构。
- 默认使用本地 `winui3-fluent-design` skill 的规则作为视觉设计依据：
  - Microsoft Learn Windows app design guidance
  - Fluent 2 Windows guidance
  - WinUI Gallery
  - Fluent XAML Theme Editor
  - Fluent UI System Icons
  - Fluent UI React Native 的 token/component anatomy 思路，仅作设计参考，不引入 React Native 实现
- 视觉系统重构的固定边界：
  - 不动业务逻辑、服务、ViewModel 行为
  - 不改现有 `NavigationView + Panel Visibility` 导航架构
  - 不把 `{Binding}` 改成 `{x:Bind}`
  - 不重命名或删除现有 `x:Name`
  - 不大规模拆分 `MainWindow.xaml`
  - 不碰 VapourSynthWorkspace 的 `EditorSurfaceHost`、`EditorWebView`、Monaco/WebView2 编辑器区域
- 允许调整的范围：
  - `App.xaml` 主题资源、Light/Dark 颜色 token、字体资源
  - XAML 中的 `Padding`、`Margin`、`CornerRadius`、`BorderBrush`、`Background`、标题层级、图标、按钮视觉、状态样式
  - 可访问性补强，例如 `AutomationProperties.Name`、tooltip、图标语义、对比度
- 视觉系统重构按三阶段执行，后续若无用户明确变更，不应跳步或改序：
  - 第一步：按项目模块逐一视觉系统重构
  - 第二步：整体项目整合
  - 第三步：项目审查、debug
- 第一步默认顺序：
  - 视觉基础层：重构 `App.xaml` 主题资源，建立 Window、Card、CardAlt、Border、SubtleText、Accent、Success、Warning、Error、Selection 等角色
  - Dashboard：调整首页 hero、模块入口卡片、图标、标题层级，保留按钮事件和现有布局
  - 视频编码 / 队列 / 自动压缩：统一表单密度、输入组、队列状态、进度区、日志区
  - 模板库：统一列表项、badge、内置/用户模板状态色，保留 `DataTemplate` 绑定
  - 音频处理 / 蓝光处理：统一路径输入、操作按钮、进度、日志、状态提示
  - 设置 / 安装向导：最后处理，重点验证 `FlipView`、依赖项卡片、ready/warning/blocked 状态色、主操作按钮
  - VapourSynthWorkspace 外壳：只处理 `CommandBar`、状态栏、日志区视觉，不动编辑器区域
  - VapourSynthPreview 弹出预览窗口：按“工具窗口 Fluent 化”处理，作为独立子模块，不与主编辑器外壳混在一起
- VapourSynthPreview 弹出预览窗口的固定策略：
  - 目标是“工具窗口 Fluent 化”，不是预览体验重写
  - 优先处理顶部 `CommandBar`、按钮 icon、tooltip、accessible name、控制面板、时间轴面板、裁剪面板、帧属性面板的视觉一致性
  - 按钮 icon 统一时优先使用 WinUI 内置图标，不足时使用 Fluent UI System Icons
  - `Reload / Save snapshot / Copy frame / Timeline / Crop / Frame props / Settings / Return` 等按钮语义必须清晰一致
  - 保持视频预览区尽量大，不用过多卡片装饰占用画面空间
  - 不改 `PreviewImage`、`PreviewScrollViewer`、`PreviewViewportHost` 的核心结构
  - 不改帧渲染、缩放、裁剪、播放、截图、复制帧逻辑
  - 不改快捷键：`F11`、`Esc`、方向键、数字键、`S`
  - 不改 `SyncControls()` 事件同步逻辑
  - 不改 `IVapourSynthPreviewService` 或 Python preview helper
  - 不重排整个预览窗口布局
- 第二步默认目标：
  - 回收重复颜色、圆角、字号、边框样式为资源
  - 统一主操作、次操作、危险操作、链接操作按钮层级
  - 统一 ready、warning、blocked、progress、disabled、empty 等状态表达
  - 统一图标策略：WinUI 内置图标优先，不足时使用 Fluent UI System Icons
  - 检查 Light/Dark 一致性、响应式布局、中文文本、长路径、日志文本、命令预览
- 第三步默认审查与 debug 清单：
  - build 通过
  - app 启动到真实响应窗口
  - 每个导航项能进入并返回
  - Dashboard、编码、模板、音频、蓝光、设置、安装向导、VapourSynth 外壳逐项点检
  - Light/Dark 切换正常
  - 高对比度或关键颜色对比检查通过
  - 图标按钮具备 label、tooltip 或 accessible name
  - 窄窗口下不重叠、不裁切关键按钮
  - 长中文、长路径、日志、命令预览不破坏布局
  - 安装向导 overlay 不遮挡、不溢出
  - VapourSynth WebView 编辑器区域不变且能加载
