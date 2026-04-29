using FlowEncode.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class ConsoleOutputLineNormalizerTests
{
    [TestMethod]
    public void Normalize_RemovesAnsiControlAndBomCharacters()
    {
        var normalized = ConsoleOutputLineNormalizer.Normalize("\uFEFF\u001b[33mEncoding:\u001b[0m 114/5400 Frames\r");

        Assert.AreEqual("Encoding: 114/5400 Frames", normalized);
    }
}
