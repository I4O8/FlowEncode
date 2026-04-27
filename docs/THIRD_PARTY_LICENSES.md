# FlowEncode Third-Party Licenses

This document records the public third-party components and tools that FlowEncode may bundle, invoke, detect, or guide users to install.

FlowEncode's own source code is distributed under `GPL-3.0-only`. That license applies to this repository's source code, not to independent runtimes, NuGet packages, encoders, media tools, VapourSynth plugins, or user-installed tools. Each third-party component remains governed by its own license.

## Bundled Or Build-Time Components

| Component | Role | License / Terms |
| --- | --- | --- |
| Monaco Editor | Bundled editor assets under `FlowEncode/Assets/VapourSynthEditor/vendor/monaco` | MIT |
| CommunityToolkit.Mvvm | MVVM helpers | MIT |
| Microsoft.Extensions.Hosting | Application hosting and dependency injection | MIT |
| SharpCompress | Archive handling | MIT |
| Microsoft.WindowsAppSDK | Windows app framework package/runtime | Microsoft license terms for the package and runtime |
| Microsoft.Windows.SDK.BuildTools | Windows SDK build tooling | Microsoft license terms |
| WebView2 Runtime / SDK surface | Runtime used by the editor host through Windows App SDK / WebView2 APIs | Microsoft license terms |
| MSTest packages | Test-only packages | MIT |

## Public External Toolchain

| Component | Role | License / Terms |
| --- | --- | --- |
| Python 3.12 | Script runtime | Python Software Foundation License |
| VapourSynth / VSPipe | Script runtime and pipe output | LGPL-2.1 |
| vsrepo | VapourSynth package helper | MIT |
| awsmfunc | VapourSynth helper package | MIT |
| vs-jetpack | VapourSynth helper package | MIT |
| VapourSynth plugins | User-installed processing plugins | Follow each plugin's own license |
| x264 | Video encoder | GPL-2.0-or-later |
| x265 | Video encoder | GPL-2.0 or commercial license, depending on distribution |
| SVT-AV1 | Video encoder | BSD 3-Clause Clear |
| FFmpeg / FFprobe | Media processing and probing tools | LGPL/GPL, depending on build configuration |
| Av1an | Target-quality encoding orchestration | GPL-3.0 |
| Avs2PipeMod | `.avs` input bridge | GPL-3.0 |

## Release Checklist

- Release assets do not bundle the repository `LICENSE` or this `THIRD_PARTY_LICENSES.md` file; these notices are maintained in the public repository.
- README and release notes must only mention public, license-clear, and publicly usable tools.
- README and release notes must not present locally supplied, private, non-open, unauthorized, or license-unclear tools as supported public dependencies.
- Release notes must not include UI details, internal implementation details, build commands, validation output, test results, SHA256 values, checksum logs, or internal QA notes.
