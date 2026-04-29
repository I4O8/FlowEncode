using FlowEncode.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class ToolLogLineClassifierTests
{
    [TestMethod]
    public void IsAutoCompressionTransientLine_TreatsAv1anPercentTickerAsTransient()
    {
        var result = ToolLogLineClassifier.IsAutoCompressionTransientLine("chunk 14/120 | encode | 11.7% | 85.3 fps");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAudioTransientLine_TreatsOpusTickerAsTransient()
    {
        var result = ToolLogLineClassifier.IsAudioTransientLine(
            AudioProcessingMode.Opus,
            "[|] 00:00:12.34 160.0 kbit/s");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsBluRayTransientLine_TreatsDgDemuxPercentTickerAsTransient()
    {
        var result = ToolLogLineClassifier.IsBluRayTransientLine(BluRayDemuxBackend.DgDemux, "54.7%");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsAutoCompressionTransientLine_DoesNotTreatStaticSummaryLineAsTransient()
    {
        var result = ToolLogLineClassifier.IsAutoCompressionTransientLine("Finished probing target quality for 12 chunks.");

        Assert.IsFalse(result);
    }
}
