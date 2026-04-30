using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class LocalEncodingJobRunnerProgressParsingTests
{
    [TestMethod]
    public void ParseProgressSnapshotForTesting_WithOfficialSvtAnsiTicker_ParsesProgressMetrics()
    {
        const string line = "Encoding: \u001b[33m 114/5400 Frames\u001b[0m @ \u001b[32m170.28\u001b[0m fps | \u001b[35m1108.11 kb/s\u001b[0m | Size: \u001b[31m1.19 MB\u001b[0m \u001b[38;5;248m[56.37 MB]\u001b[0m | Time: \u001b[36m0:00:01\u001b[0m \u001b[38;5;248m[-0:00:31]\u001b[0m";

        var parsed = LocalEncodingJobRunner.ParseProgressSnapshotForTesting(
            EncoderKind.SvtAv1,
            totalFrames: 5400,
            sourceFramesPerSecond: 24000d / 1001d,
            line);

        Assert.IsNotNull(parsed.Snapshot);
        Assert.AreEqual(114L, parsed.Snapshot.CurrentFrame);
        Assert.AreEqual(5400L, parsed.Snapshot.TotalFrames);
        Assert.AreEqual(170.28, parsed.Snapshot.FramesPerSecond!.Value, 0.001);
        Assert.AreEqual(1108.11, parsed.Snapshot.BitrateKbps!.Value, 0.001);
        Assert.AreEqual(TimeSpan.FromSeconds(31), parsed.Snapshot.Eta);
        Assert.IsTrue(parsed.Snapshot.EstimatedFileSizeBytes > 0);
        Assert.AreEqual(114d / 5400d, parsed.ProgressFraction!.Value, 0.000001);
    }

    [TestMethod]
    public void ParseSourcePreparationProgressPercentForTesting_WithLwiIndexLine_ParsesPercent()
    {
        const string line = "Creating lwi index file 42%";

        var parsed = LocalEncodingJobRunner.ParseSourcePreparationProgressPercentForTesting(line);

        Assert.AreEqual(42, parsed);
    }

    [TestMethod]
    public void ParseSourcePreparationProgressPercentForTesting_WithBestSourceIndexLine_ParsesPercent()
    {
        const string line = "Information: VideoSource track #0 index progress 54%";

        var parsed = LocalEncodingJobRunner.ParseSourcePreparationProgressPercentForTesting(line);

        Assert.AreEqual(54, parsed);
    }

    [TestMethod]
    public void ParseSourcePreparationProgressPercentForTesting_WithUnrelatedLine_ReturnsNull()
    {
        const string line = "Script evaluation finished.";

        var parsed = LocalEncodingJobRunner.ParseSourcePreparationProgressPercentForTesting(line);

        Assert.IsNull(parsed);
    }
}
