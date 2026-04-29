using FlowEncode.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class EncodingLogLineClassifierTests
{
    [TestMethod]
    public void IsTransientProgressLine_TreatsX264PipeTickerAsProgress()
    {
        var result = EncodingLogLineClassifier.IsTransientProgressLine(
            EncoderKind.X264,
            "x264 3025 frames @ 46.49 fps | 13974 kb/s | 220.4 MB");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsTransientProgressLine_TreatsX265PipeTickerAsProgress()
    {
        var result = EncodingLogLineClassifier.IsTransientProgressLine(
            EncoderKind.X265,
            "x265 3025 frames @ 46.49 fps | 13974 kb/s | 220.4 MB");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsTransientProgressLine_TreatsOfficialSvtAnsiTickerAsProgress()
    {
        var result = EncodingLogLineClassifier.IsTransientProgressLine(
            EncoderKind.SvtAv1,
            "Encoding: \u001b[33m 114/5400 Frames\u001b[0m @ \u001b[32m170.28\u001b[0m fps | \u001b[35m1108.11 kb/s\u001b[0m | Size: \u001b[31m1.19 MB\u001b[0m \u001b[38;5;248m[56.37 MB]\u001b[0m | Time: \u001b[36m0:00:01\u001b[0m \u001b[38;5;248m[-0:00:31]\u001b[0m");

        Assert.IsTrue(result);
    }
}
