namespace FlowEncode.Domain;

public static class DefaultEncodingProfiles
{
    private static readonly EncodingProfile X264Profile = new(
        EncoderKind.X264,
        "x264 默认参数",
        "通用 AVC 参数，默认 8-bit 输出，CRF + veryslow + High。",
        "veryslow",
        string.Empty,
        "High",
        RateControlMode.Crf,
        18,
        null,
        "264",
        "--level 4.1 --vbv-bufsize 62500 --vbv-maxrate 78125 --ref 4 --merange 32 --bframes 16 --deblock -3:-3 --no-fast-pskip --qcomp 0.60 --psy-rd 1.00:0.00 --aq-mode 3 --aq-strength 0.80 --no-mbtree --ipratio 1.30 --pbratio 1.20 --keyint 250 --min-keyint 23 --no-dct-decimate",
        string.Empty);

    private static readonly EncodingProfile X265Profile = new(
        EncoderKind.X265,
        "x265 默认参数",
        "通用 HEVC 参数，默认 10-bit 输出，CRF + veryslow + main10。",
        "veryslow",
        string.Empty,
        "main10",
        RateControlMode.Crf,
        18,
        null,
        "hevc",
        "--level-idc 5.1 --bframes 12 --rd 4 --me 3 --subme 7 --ref 5 --hrd --merange 57 --ipratio 1.3 --pbratio 1.2 --aq-mode 3 --aq-strength 0.90 --qcomp 0.60 --psy-rd 1.5 --psy-rdoq 1.00 --rdoq-level 2 --rc-lookahead 100 --deblock -3:-3 --no-strong-intra-smoothing --cbqpoffs -2 --crqpoffs -2 --qg-size 8 --no-frame-dup --no-cutree --tu-intra-depth 4 --no-open-gop --tu-inter-depth 4 --rskip 0 --no-tskip --no-early-skip --min-keyint=1 --vbv-bufsize 160000 --vbv-maxrate 160000 --no-sao --aud --repeat-headers",
        string.Empty);

    private static readonly EncodingProfile SvtAv1Profile = new(
        EncoderKind.SvtAv1,
        "AV1 默认参数",
        "通用 SVT-AV1 参数，默认 CRF + preset 3 + VQ，输入位深跟随源信息。",
        "3",
        "VQ",
        "main",
        RateControlMode.Crf,
        20,
        null,
        "ivf",
        "--lookahead 120 --enable-overlays 1 --enable-restoration 1 --enable-dlf 2 --enable-cdef 1 --enable-stat-report 1",
        string.Empty);

    public static EncodingProfile GetDefault(EncoderKind kind)
    {
        return kind switch
        {
            EncoderKind.X264 => X264Profile,
            EncoderKind.X265 => X265Profile,
            EncoderKind.SvtAv1 => SvtAv1Profile,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }
}
