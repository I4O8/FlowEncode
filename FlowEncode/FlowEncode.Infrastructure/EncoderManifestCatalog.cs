using FlowEncode.Domain;

namespace FlowEncode.Infrastructure;

internal static class EncoderManifestCatalog
{
    private static readonly IReadOnlyList<string> X26xPresets =
    [
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow", "placebo"
    ];

    private static readonly IReadOnlyList<EncoderCapability> Capabilities =
    [
        new EncoderCapability(
            EncoderKind.X264,
            "Legacy AVC workhorse with mature tuning and broad device compatibility. The preset list follows the Simple x264 Launcher baseline.",
            X26xPresets,
            ["Film", "Animation", "Grain", "StillImage", "PSNR", "SSIM", "FastDecode", "ZeroLatency", "Touhou"],
            ["Baseline", "Main", "High", "High10", "High422", "High444"],
            [RateControlMode.Crf, RateControlMode.Cq, RateControlMode.TwoPass, RateControlMode.Abr],
            ["264", "mkv", "mp4"],
            false,
            [
                new EncoderUpdateChannel(
                    "Patman86 x264 发布页",
                    "https://github.com/Patman86/x264-Mod-by-Patman/releases/latest",
                    "FlowEncode 默认回退到这个 x264 发布源，便于统一自动更新与手动导入策略。"),
                new EncoderUpdateChannel(
                    "VideoLAN 源码",
                    "https://code.videolan.org/videolan/x264",
                    "用于核对上游源码变更与参数兼容性。")
            ]),
        new EncoderCapability(
            EncoderKind.X265,
            "HEVC 编码主力，保留 x265 的预设、tune 和 profile 组合，适合高压缩率归档与 Win11 原生播放器分发。",
            X26xPresets,
            ["Grain", "PSNR", "SSIM", "FastDecode", "ZeroLatency", "Animation"],
            ["main", "main10", "mainstillpicture"],
            [RateControlMode.Crf, RateControlMode.Cq, RateControlMode.TwoPass, RateControlMode.Abr],
            ["hevc", "mkv", "mp4"],
            false,
            [
                new EncoderUpdateChannel(
                    "Patman86 x265 发布页",
                    "https://github.com/Patman86/x265-Mod-by-Patman/releases/latest",
                    "FlowEncode 默认回退到这个 x265 发布源，优先拿通用 x86-64 GCC 构建。"),
                new EncoderUpdateChannel(
                    "MulticoreWare 仓库",
                    "https://bitbucket.org/multicoreware/x265_git",
                    "用于追踪官方源码更新。")
            ]),
        new EncoderCapability(
            EncoderKind.SvtAv1,
            "AV1 编码通道。preset、tune 与 profile 集合已对齐 SVT-AV1 官方参数定义，并为后续扩展到更完整的 AV1 工作流预留结构。",
            ["13", "12", "11", "10", "9", "8", "7", "6", "5", "4", "3", "2", "1", "0", "-1"],
            ["VQ", "PSNR", "SSIM", "IQ", "MS-SSIM"],
            ["main", "high", "professional"],
            [RateControlMode.Crf, RateControlMode.Qp, RateControlMode.TwoPass, RateControlMode.Vbr],
            ["ivf", "mkv"],
            true,
            [
                new EncoderUpdateChannel(
                    "Patman86 SVT-AV1 发布页",
                    "https://github.com/Patman86/SVT-AV1-Mods-by-Patman/releases/latest",
                    "FlowEncode 默认回退到这个 SVT-AV1 EncApp 发布源。"),
                new EncoderUpdateChannel(
                    "AOMedia GitLab",
                    "https://gitlab.com/AOMediaCodec/SVT-AV1",
                    "用于追踪官方 release 和 changelog。")
            ])
    ];

    public static IReadOnlyList<EncoderCapability> GetAll() => Capabilities;
}
