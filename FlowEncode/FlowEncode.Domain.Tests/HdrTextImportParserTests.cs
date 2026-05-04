using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class HdrTextImportParserTests
{
    [TestMethod]
    public void Parse_WithBdInfoStyleText_GeneratesExpectedX265Arguments()
    {
        const string rawText = """
Color primaries : BT.2020
Transfer characteristics : PQ
Matrix coefficients : BT.2020 non-constant
Mastering display color primaries : Display P3
Mastering display luminance : min: 0.0050 cd/m2, max: 1000 cd/m2
Maximum Content Light Level : 1000
Maximum Frame-Average Light Level : 400
""";

        var result = HdrTextImportParser.Parse(rawText);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(
            "--colorprim bt2020 --transfer smpte2084 --colormatrix bt2020nc --master-display \"G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,50)\" --max-cll \"1000,400\"",
            result.Arguments);
    }

    [TestMethod]
    public void Parse_WithMediaInfoStyleText_GeneratesExpectedX265Arguments()
    {
        const string rawText = """
Color range : Limited
Color primaries : BT.2020
Transfer characteristics : SMPTE ST 2084
Matrix coefficients : BT.2020 non-constant
Mastering display color primaries : BT.2020
Mastering display luminance : min: 0.0200 cd/m2, max: 4000 cd/m2
MaxCLL / MaxFALL : 1000 / 400
""";

        var result = HdrTextImportParser.Parse(rawText);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(
            "--range limited --colorprim bt2020 --transfer smpte2084 --colormatrix bt2020nc --master-display \"G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(40000000,200)\" --max-cll \"1000,400\"",
            result.Arguments);
    }

    [TestMethod]
    public void Parse_WithMediaInfoThousandSeparatedLightLevels_GeneratesExpectedX265Arguments()
    {
        const string rawText = """
Color range                              : Limited
Color primaries                          : BT.2020
Transfer characteristics                 : PQ
Matrix coefficients                      : BT.2020 non-constant
Mastering display color primaries        : BT.2020
Mastering display luminance              : min: 0.0050 cd/m2, max: 1000 cd/m2
Maximum Content Light Level              : 1 000 cd/m2
Maximum Frame-Average Light Level        : 184 cd/m2
""";

        var result = HdrTextImportParser.Parse(rawText);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(
            "--range limited --colorprim bt2020 --transfer smpte2084 --colormatrix bt2020nc --master-display \"G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(10000000,50)\" --max-cll \"1000,184\"",
            result.Arguments);
    }

    [TestMethod]
    public void Parse_WithCompactMediaInfoFormatting_GeneratesExpectedX265Arguments()
    {
        const string rawText = """
Color range: Limited
Color primaries: BT.2020
Transfer characteristics: PQ
Matrix coefficients: BT.2020 non-constant
Mastering display color primaries: BT.2020
Mastering display luminance: min: 0.0050 cd/m2, max: 1000 cd/m2
Maximum Content Light Level: 1 000 cd/m2
Maximum Frame-Average Light Level: 184 cd/m2
""";

        var result = HdrTextImportParser.Parse(rawText);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(
            "--range limited --colorprim bt2020 --transfer smpte2084 --colormatrix bt2020nc --master-display \"G(8500,39850)B(6550,2300)R(35400,14600)WP(15635,16450)L(10000000,50)\" --max-cll \"1000,184\"",
            result.Arguments);
    }

    [TestMethod]
    public void Parse_WhenOnlySignalFieldsExist_EmitsPartialArguments()
    {
        const string rawText = """
Color range : Limited
Color primaries : BT.2020
Transfer characteristics : PQ
Matrix coefficients : BT.2020 non-constant
""";

        var result = HdrTextImportParser.Parse(rawText);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(
            "--range limited --colorprim bt2020 --transfer smpte2084 --colormatrix bt2020nc",
            result.Arguments);
    }

    [TestMethod]
    public void Parse_WithUnrelatedText_ReturnsFailure()
    {
        var result = HdrTextImportParser.Parse("hello world");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(string.Empty, result.Arguments);
    }
}
