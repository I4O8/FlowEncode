# FlowEncode Release Notes Template

本文件用于统一 GitHub Release 的更新说明写法。默认规则以 [RELEASE_PROCESS.md](./RELEASE_PROCESS.md) 中的 `FlowEncode Push & Release Rules` 为准。

## Default Structure

除非本次发布体量明显较大，否则 release notes 默认使用以下结构：

```md
## FlowEncode <version>

FlowEncode <version> 是一个面向 Windows x64 桌面版的<release type>版本。
本次更新重点围绕<primary functional focus>展开，并继续保持仅安装版单资产分发。

### Highlights

- <functional change 1>
- <functional change 2>
- <functional change 3>

### Upgrade Notes

- Windows x64 only
- Only ship `FlowEncode-Setup.exe`
- <migration / cache / compatibility note if any>
```

## Release Type Vocabulary

- `功能更新版本`：引入新的工作流能力或明显扩展现有模块
- `维护更新版本`：以公开功能范围内的行为校正、依赖边界或兼容性维护为主
- `问题修复版本`：以问题修复和行为校正为主
- `热修复版本`：针对已发布版本中的高优先级回归或阻塞问题

## Writing Rules

- 先写版本定位，再写本次更新重点
- `Highlights` 只写功能性新增、功能性变更、功能性修正或功能移除
- 不写 UI 布局调整、视觉细节、首页卡片高度、组件摆放或样式整理
- 不写内部逻辑整理、重构、打包脚本健壮性、构建流程或实现过程
- 不写构建命令、构建通过信息、测试结果、启动验证、内部 QA、SHA256 或其他 checksum 信息
- 不写非开源、非授权、授权不清、需要用户自行私有取得或仅供本地实验的工具
- 默认保持精简，不堆章节，不做流水账
- 只有当发布范围确实很大时，才允许在 `Highlights` 和 `Upgrade Notes` 之间增加额外模块章节
- `Upgrade Notes` 必须显式说明 Windows x64 支持范围、当前仅提供安装版单资产，以及必要的缓存、配置或兼容性说明

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
本次更新重点加入了 VapourSynth 脚本诊断与预览能力，并继续保持仅安装版单资产分发。
```

### 维护更新版本

```md
## FlowEncode 1.1.9

FlowEncode 1.1.9 是一个面向 Windows x64 桌面版的维护更新版本。
本次更新重点修正公开工具链状态识别与配置保存行为，并继续保持仅安装版单资产分发。
```

### 问题修复版本

```md
## FlowEncode 1.1.10

FlowEncode 1.1.10 是一个面向 Windows x64 桌面版的问题修复版本。
本次更新聚焦于文件选择与输出路径保存行为，主要修正部分环境下路径选择无法完成的问题。
```

## Suggested Checklist

- release 标题与 tag 一致
- 标题和简介使用中文
- 默认结构为 `标题 + 简介 + Highlights + Upgrade Notes`
- 明确写出 Windows x64 only
- 明确写出当前仅提供安装版单资产
- 资产名未附加版本号或 `win-x64`
- 未包含 UI 布局、视觉样式、内部实现、构建流程、验证结果、测试结果、SHA 或内部 QA 信息
- 未包含非开源、非授权、授权不清、需要用户自行私有取得或仅供本地实验的工具
