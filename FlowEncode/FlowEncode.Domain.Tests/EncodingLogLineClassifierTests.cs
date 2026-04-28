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
}
