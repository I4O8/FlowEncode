using FlowEncode.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class DefaultEncodingProfilesTests
{
    [TestMethod]
    public void GetDefault_WhenX264Requested_ReturnsCleanPresetDrivenDefaults()
    {
        var profile = DefaultEncodingProfiles.GetDefault(EncoderKind.X264);

        Assert.AreEqual("veryslow", profile.Preset);
        Assert.AreEqual("High", profile.Profile);
        Assert.AreEqual(RateControlMode.Crf, profile.RateControl);
        Assert.AreEqual(18d, profile.Quality);
        Assert.AreEqual("--level 4.1 --vbv-bufsize 62500 --vbv-maxrate 78125 --ref 4 --merange 32 --bframes 16 --deblock -3:-3 --no-fast-pskip --qcomp 0.60 --psy-rd 1.00:0.00 --aq-mode 3 --aq-strength 0.80 --no-mbtree --colormatrix bt709 --colorprim bt709 --ipratio 1.30 --pbratio 1.20 --keyint 250 --min-keyint 23 --no-dct-decimate", profile.AdditionalArguments);
    }

    [TestMethod]
    public void GetDefault_WhenX265Requested_ReturnsCleanPresetDrivenDefaults()
    {
        var profile = DefaultEncodingProfiles.GetDefault(EncoderKind.X265);

        Assert.AreEqual("veryslow", profile.Preset);
        Assert.AreEqual("main10", profile.Profile);
        Assert.AreEqual(RateControlMode.Crf, profile.RateControl);
        Assert.AreEqual(18d, profile.Quality);
        Assert.AreEqual("--level-idc 5.1 --bframes 12 --rd 4 --me 3 --subme 7 --ref 5 --hrd --merange 57 --ipratio 1.3 --pbratio 1.2 --aq-mode 3 --aq-strength 0.90 --qcomp 0.60 --psy-rd 1.5 --psy-rdoq 1.00 --rdoq-level 2 --rc-lookahead 100 --deblock -3:-3 --no-strong-intra-smoothing --cbqpoffs -2 --crqpoffs -2 --qg-size 8 --range limited --no-frame-dup --no-cutree --tu-intra-depth 4 --no-open-gop --tu-inter-depth 4 --rskip 0 --no-tskip --no-early-skip --min-keyint=1 --vbv-bufsize 160000 --vbv-maxrate 160000 --no-sao --aud --repeat-headers", profile.AdditionalArguments);
        Assert.AreEqual(string.Empty, profile.UhdParameters);
    }

    [TestMethod]
    public void GetDefault_WhenSvtAv1Requested_ReturnsOfficialTuneAndNoExtraArguments()
    {
        var profile = DefaultEncodingProfiles.GetDefault(EncoderKind.SvtAv1);

        Assert.AreEqual("3", profile.Preset);
        Assert.AreEqual("VQ", profile.Tune);
        Assert.AreEqual("main", profile.Profile);
        Assert.AreEqual(RateControlMode.Crf, profile.RateControl);
        Assert.AreEqual(20d, profile.Quality);
        Assert.AreEqual("--lookahead 120 --enable-overlays 1 --enable-restoration 1 --enable-dlf 2 --enable-cdef 1 --transfer-characteristics 1 --matrix-coefficients 1 --color-primaries 1 --enable-stat-report 1", profile.AdditionalArguments);
    }

    [TestMethod]
    public void NormalizeTuneForCli_WithSvtAv1Aliases_ReturnsOfficialNumericValue()
    {
        var cases = new (string Input, string Expected)[]
        {
            ("VQ", "0"),
            ("IQ", "3"),
            ("MS-SSIM", "4"),
            ("Still Picture", "4")
        };

        foreach (var (input, expected) in cases)
        {
            var normalized = EncoderArgumentValueNormalizer.NormalizeTuneForCli(EncoderKind.SvtAv1, input);
            Assert.AreEqual(expected, normalized, $"Input: {input}");
        }
    }

    [TestMethod]
    public void NormalizeTuneForUi_WithSvtAv1NumericValues_ReturnsOfficialDisplayLabel()
    {
        var cases = new (string Input, string Expected)[]
        {
            ("0", "VQ"),
            ("3", "IQ"),
            ("4", "MS-SSIM")
        };

        foreach (var (input, expected) in cases)
        {
            var normalized = EncoderArgumentValueNormalizer.NormalizeTuneForUi(EncoderKind.SvtAv1, input);
            Assert.AreEqual(expected, normalized, $"Input: {input}");
        }
    }
}
