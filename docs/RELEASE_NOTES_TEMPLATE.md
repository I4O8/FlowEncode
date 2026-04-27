# FlowEncode Release Notes Template

本文件用于统一 GitHub Release 的更新说明写法。默认规则以 [PROJECT_MEMORY.md](/D:/codex/FlowEncode/docs/PROJECT_MEMORY.md) 中的 `Release Memory` 和 `Release Notes Memory` 为准。

## Default Structure

除非本次发布体量明显较大，否则 release notes 默认使用以下结构：

```md
## FlowEncode <version>

FlowEncode <version> 是一个面向 Windows x64 桌面版的<release type>版本。
本次更新重点围绕<primary focus>展开，并继续保持仅安装版单资产分发。

### Highlights

- <user-facing change 1>
- <user-facing change 2>
- <user-facing change 3>

### Upgrade Notes

- Windows x64 only
- Only ship `FlowEncode-Setup.exe`
- <migration / cache / compatibility note if any>
```

## Release Type Vocabulary

- `功能更新版本`：引入新的工作流能力或明显扩展现有模块
- `维护更新版本`：以稳定性、结构整理、体验一致性为主
- `问题修复版本`：以问题修复和行为校正为主
- `热修复版本`：针对已发布版本中的高优先级回归或阻塞问题

## Writing Rules

- 先写版本定位，再写本次更新重点
- `Highlights` 只写用户可感知结果，不写开发过程和试错过程
- 默认保持精简，不堆章节，不做流水账
- 只有当发布范围确实很大时，才允许在 `Highlights` 和 `Upgrade Notes` 之间增加额外模块章节
- `Upgrade Notes` 必须显式说明 Windows x64 支持范围，以及当前仅提供安装版单资产

## Asset Rules

- GitHub Release 标题固定为 `FlowEncode <version>`
- tag 固定为 `v<version>`
- 资产名固定为：
  - `FlowEncode-Setup.exe`
- 不再附加版本号、`win-x64` 或其他 RID 后缀

## Intro Examples

### 功能更新版本

```md
## FlowEncode <version>

FlowEncode <version> 是一个面向 Windows x64 桌面版的功能更新版本。
本次更新重点加入了蓝光盘解复用工作流，并继续保持仅安装版单资产分发。
```

### 维护更新版本

```md
## FlowEncode 1.1.9

FlowEncode 1.1.9 是一个面向 Windows x64 桌面版的维护更新版本。
本次更新重点围绕首启状态持久化、依赖检测行为与打包一致性进行整理，并继续保持仅安装版单资产分发。
```

### 问题修复版本

```md
## FlowEncode 1.1.10

FlowEncode 1.1.10 是一个面向 Windows x64 桌面版的问题修复版本。
本次更新聚焦于安装版分发质量，主要修正 `Setup.exe` 的图标显示问题。
```

## Suggested Checklist

- release 标题与 tag 一致
- 标题和简介使用中文
- 默认结构为 `标题 + 简介 + Highlights + Upgrade Notes`
- 明确写出 Windows x64 only
- 明确写出当前仅提供安装版单资产
- 资产名未附加版本号或 `win-x64`
