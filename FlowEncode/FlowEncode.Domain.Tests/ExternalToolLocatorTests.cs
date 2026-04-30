using FlowEncode.Domain;
using FlowEncode.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class ExternalToolLocatorTests
{
    [TestMethod]
    public void BuildMissingToolMessage_ForFfmpegInEnglish_UsesEnglishCopy()
    {
        var message = ExternalToolLocator.BuildMissingToolMessage(RegisteredToolKind.Ffmpeg, AppLanguage.English);

        StringAssert.Contains(message, "ffmpeg.exe was not found.");
        StringAssert.Contains(message, "Install FFmpeg");
        Assert.IsFalse(message.Contains("未找到", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildMissingToolMessage_ForVspipeInChinese_UsesChineseCopy()
    {
        var message = ExternalToolLocator.BuildMissingToolMessage(RegisteredToolKind.Vspipe, AppLanguage.Chinese);

        StringAssert.Contains(message, "未找到可用的 vspipe.exe。");
        StringAssert.Contains(message, "请先安装 VapourSynth");
        Assert.IsFalse(message.Contains("was not found", StringComparison.Ordinal));
    }
}
