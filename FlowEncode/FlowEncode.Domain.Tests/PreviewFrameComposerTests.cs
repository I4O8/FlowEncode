using FlowEncode.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowEncode.Domain.Tests;

[TestClass]
public sealed class PreviewFrameComposerTests
{
    private readonly PreviewFrameComposer _composer = new();

    [TestMethod]
    public void Compose_WhenCropDisabled_ReturnsSourcePixels()
    {
        var sourcePixels = BuildPixels(4, 3);
        var request = CreateRequest(sourcePixels, 4, 3, cropEnabled: false);

        var result = _composer.Compose(request);

        Assert.AreSame(sourcePixels, result.Pixels);
        Assert.AreEqual(4, result.Width);
        Assert.AreEqual(3, result.Height);
        Assert.IsFalse(result.IsCropped);
        Assert.AreEqual(0, result.CropBounds.Left);
        Assert.AreEqual(0, result.CropBounds.Top);
    }

    [TestMethod]
    public void Compose_WhenAbsoluteCropMatchesFullFrame_ReturnsSourcePixels()
    {
        var sourcePixels = BuildPixels(4, 3);
        var request = new PreviewFrameCompositionRequest(
            sourcePixels,
            4,
            3,
            new PreviewFrameCropSettings(
                true,
                PreviewCropMode.Absolute,
                new PreviewAbsoluteCrop(0, 0, 4, 3),
                new PreviewRelativeCrop(0, 0, 0, 0)));

        var result = _composer.Compose(request);

        Assert.AreSame(sourcePixels, result.Pixels);
        Assert.AreEqual(4, result.Width);
        Assert.AreEqual(3, result.Height);
        Assert.IsFalse(result.IsCropped);
    }

    [TestMethod]
    public void Compose_WhenAbsoluteCropRequested_CropsPixelsIntoReusableBuffer()
    {
        var sourcePixels = BuildPixels(4, 3);
        var request = new PreviewFrameCompositionRequest(
            sourcePixels,
            4,
            3,
            new PreviewFrameCropSettings(
                true,
                PreviewCropMode.Absolute,
                new PreviewAbsoluteCrop(1, 1, 2, 2),
                new PreviewRelativeCrop(0, 0, 0, 0)));
        var reusableBuffer = new byte[2 * 2 * 4];

        var result = _composer.Compose(request, reusableBuffer);

        Assert.AreSame(reusableBuffer, result.Pixels);
        CollectionAssert.AreEqual(
            SlicePixels(sourcePixels, 4, 1, 1, 2, 2),
            result.Pixels);
        Assert.AreEqual(2, result.Width);
        Assert.AreEqual(2, result.Height);
        Assert.IsTrue(result.IsCropped);
    }

    [TestMethod]
    public void ResolveCropBounds_WhenRelativeCropRequested_ClampsToFrameBounds()
    {
        var request = new PreviewFrameCompositionRequest(
            BuildPixels(4, 3),
            4,
            3,
            new PreviewFrameCropSettings(
                true,
                PreviewCropMode.Relative,
                new PreviewAbsoluteCrop(0, 0, 0, 0),
                new PreviewRelativeCrop(2, 1, 9, 9)));

        var bounds = _composer.ResolveCropBounds(request.Crop, request.SourceWidth, request.SourceHeight);

        Assert.AreEqual(2, bounds.Left);
        Assert.AreEqual(1, bounds.Top);
        Assert.AreEqual(1, bounds.Width);
        Assert.AreEqual(1, bounds.Height);
        Assert.AreEqual(1, bounds.Right);
        Assert.AreEqual(1, bounds.Bottom);
    }

    private static PreviewFrameCompositionRequest CreateRequest(
        byte[] sourcePixels,
        int width,
        int height,
        bool cropEnabled)
    {
        return new PreviewFrameCompositionRequest(
            sourcePixels,
            width,
            height,
            new PreviewFrameCropSettings(
                cropEnabled,
                PreviewCropMode.Relative,
                new PreviewAbsoluteCrop(0, 0, width, height),
                new PreviewRelativeCrop(0, 0, 0, 0)));
    }

    private static byte[] BuildPixels(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i % 251);
        }

        return pixels;
    }

    private static byte[] SlicePixels(byte[] sourcePixels, int sourceWidth, int left, int top, int width, int height)
    {
        var targetPixels = new byte[width * height * 4];
        const int bytesPerPixel = 4;
        var targetStride = width * bytesPerPixel;

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = ((top + row) * sourceWidth + left) * bytesPerPixel;
            var targetOffset = row * targetStride;
            Buffer.BlockCopy(sourcePixels, sourceOffset, targetPixels, targetOffset, targetStride);
        }

        return targetPixels;
    }
}
