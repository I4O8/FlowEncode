# FlowEncode Release Process

本流程用于生成当前项目版本对应的正式资产。

## Output Contract

- Git tag: `v<version>`
- GitHub Release 标题: `FlowEncode <version>`
- 资产:
  - `FlowEncode-Setup.exe`

## Build Command

在仓库根目录执行：

```powershell
.\scripts\build-release-assets.ps1
```

如需显式传入版本号，参数值必须与项目文件中的 `<Version>` 完全一致：

```powershell
.\scripts\build-release-assets.ps1 -Version 1.3.0
```

默认产物位置：

```text
artifacts\release\v<version>\
  FlowEncode-Setup.exe
```

## Notes

- 安装版由 [FlowEncode.iss](/D:/codex/FlowEncode/installer/FlowEncode.iss) 通过 Inno Setup 生成
- 构建脚本会先在中间目录发布应用，再基于该发布结果生成最终安装版
- 资产名不再带 `win-x64`
- 构建脚本会自动清理运行时目录、日志、缓存、PDB 和非保留语言目录
- 如需重发某个已存在版本，应先删除现有 release 和 tag，再重新构建并发布
