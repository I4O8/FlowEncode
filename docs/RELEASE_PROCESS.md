# FlowEncode Push & Release Rules

本规则用于约束 FlowEncode 仓库的公开文本、提交内容与 GitHub Release 发布说明。

## Repository Hygiene

- 不提交、不上传本地项目记忆、个人思路整理、临时计划或草稿文档。
- `docs/PROJECT_MEMORY.md` 属于本地记忆文档，必须保持在 `.gitignore` 中，不能重新加入仓库。
- README、release notes、第三方许可证说明和发布流程文档是公开材料，应只写可公开验证、可长期维护的内容。
- 仓库文本不得把本地自备、授权不清、非公开可用或非授权分发的工具写成公开支持能力。

## Public Documentation Rules

- README 只描述公开支持的功能范围、发行方式、依赖边界、隐私边界和许可证边界。
- README 不写布局、视觉样式、组件摆放、交互细节、内部实现、重构过程、构建脚本细节或验证结果。
- README 只列公开可用且许可证清晰的外部工具链组件。
- 非开源、非授权、授权不清、需要用户自行私有取得或仅供本地实验的工具，不得出现在 README 和 release notes 中。
- 第三方许可证说明只纳入公开支持范围内的组件；不确定授权状态的工具不纳入公开支持范围。

## Release Notes Rules

- Release notes 只写功能性新增、功能性变更、功能性修正或功能移除。
- 不写 UI 布局调整、视觉细节、首页卡片高度、内部逻辑整理、重构、打包脚本健壮性、构建流程或实现过程。
- 不写构建命令、构建通过信息、测试结果、启动验证、内部 QA、校验日志、SHA256 或其他 checksum 信息。
- 不写非开源、非授权、授权不清、需要用户自行私有取得或仅供本地实验的工具。
- `Upgrade Notes` 只写用户升级所需的信息，例如系统范围、资产形态、缓存迁移、配置迁移或兼容性说明。

## Output Contract

- Git tag: `v<version>`
- GitHub Release 标题: `FlowEncode <version>`
- GitHub Release 资产:
  - `FlowEncode-Setup.exe`

## Build Command

在仓库根目录执行：

```powershell
.\scripts\build-release-assets.ps1
```

如需显式传入版本号，参数值必须与项目文件中的 `<Version>` 完全一致：

```powershell
.\scripts\build-release-assets.ps1 -Version 1.6.1
```

默认产物位置：

```text
artifacts\release\v<version>\
  FlowEncode-Setup.exe
```

## Release Checklist

- README 没有本地记忆、UI 调整、内部实现、构建验证或授权不清工具的公开表述。
- Release notes 只保留功能性变更，不包含 UI、构建、验证、测试、SHA 或内部 QA 信息。
- `docs/THIRD_PARTY_LICENSES.md` 已覆盖公开支持范围内的第三方组件，作为仓库公开说明保留。
- 安装资产不额外携带仓库 `LICENSE` 或 `THIRD_PARTY_LICENSES.md` 说明文件。
- 资产名固定为 `FlowEncode-Setup.exe`，不附加版本号、`win-x64` 或其他 RID 后缀。
- 如需重发某个已存在版本，先删除现有 release 和 tag，再重新构建并发布。
