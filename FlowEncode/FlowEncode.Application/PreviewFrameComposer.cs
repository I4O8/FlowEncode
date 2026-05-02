namespace FlowEncode.Application;

public sealed class PreviewFrameComposer
{
    private const int BytesPerPixel = 4;

    public PreviewFrameCompositionResult Compose(
        PreviewFrameCompositionRequest request,
        byte[]? reusableBuffer = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourcePixels);

        var sourceWidth = Math.Max(1, request.SourceWidth);
        var sourceHeight = Math.Max(1, request.SourceHeight);
        var sourceLength = checked(sourceWidth * sourceHeight * BytesPerPixel);
        if (request.SourcePixels.Length < sourceLength)
        {
            throw new ArgumentException("Pixel buffer is shorter than the source frame size.", nameof(request));
        }

        var cropBounds = request.Crop.IsEnabled
            ? ResolveCropBounds(request.Crop, sourceWidth, sourceHeight)
            : new PreviewCropBounds(0, 0, sourceWidth, sourceHeight, 0, 0);

        if (!request.Crop.IsEnabled || IsFullFrameCrop(cropBounds, sourceWidth, sourceHeight))
        {
            return new PreviewFrameCompositionResult(
                request.SourcePixels,
                sourceWidth,
                sourceHeight,
                cropBounds,
                false);
        }

        var targetLength = checked(cropBounds.Width * cropBounds.Height * BytesPerPixel);
        var targetPixels = reusableBuffer is { Length: var reusableLength } && reusableLength == targetLength
            ? reusableBuffer
            : GC.AllocateUninitializedArray<byte>(targetLength);

        var targetStride = cropBounds.Width * BytesPerPixel;
        for (var row = 0; row < cropBounds.Height; row++)
        {
            var sourceOffset = ((cropBounds.Top + row) * sourceWidth + cropBounds.Left) * BytesPerPixel;
            var targetOffset = row * targetStride;
            Buffer.BlockCopy(request.SourcePixels, sourceOffset, targetPixels, targetOffset, targetStride);
        }

        return new PreviewFrameCompositionResult(
            targetPixels,
            cropBounds.Width,
            cropBounds.Height,
            cropBounds,
            true);
    }

    public PreviewCropBounds ResolveCropBounds(
        PreviewFrameCropSettings crop,
        int sourceWidth,
        int sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(crop);

        sourceWidth = Math.Max(1, sourceWidth);
        sourceHeight = Math.Max(1, sourceHeight);

        return crop.Mode switch
        {
            PreviewCropMode.Absolute => ResolveAbsoluteCropBounds(crop.Absolute, sourceWidth, sourceHeight),
            _ => ResolveRelativeCropBounds(crop.Relative, sourceWidth, sourceHeight)
        };
    }

    private static PreviewCropBounds ResolveAbsoluteCropBounds(
        PreviewAbsoluteCrop crop,
        int sourceWidth,
        int sourceHeight)
    {
        var left = Math.Clamp(crop.Left, 0, sourceWidth - 1);
        var top = Math.Clamp(crop.Top, 0, sourceHeight - 1);
        var width = Math.Clamp(crop.Width, 1, sourceWidth - left);
        var height = Math.Clamp(crop.Height, 1, sourceHeight - top);
        var right = Math.Max(0, sourceWidth - left - width);
        var bottom = Math.Max(0, sourceHeight - top - height);

        return new PreviewCropBounds(left, top, width, height, right, bottom);
    }

    private static PreviewCropBounds ResolveRelativeCropBounds(
        PreviewRelativeCrop crop,
        int sourceWidth,
        int sourceHeight)
    {
        var left = Math.Clamp(crop.Left, 0, sourceWidth - 1);
        var top = Math.Clamp(crop.Top, 0, sourceHeight - 1);
        var right = Math.Clamp(crop.Right, 0, Math.Max(0, sourceWidth - left - 1));
        var bottom = Math.Clamp(crop.Bottom, 0, Math.Max(0, sourceHeight - top - 1));
        var width = Math.Max(1, sourceWidth - left - right);
        var height = Math.Max(1, sourceHeight - top - bottom);

        return new PreviewCropBounds(left, top, width, height, right, bottom);
    }

    private static bool IsFullFrameCrop(
        PreviewCropBounds cropBounds,
        int sourceWidth,
        int sourceHeight)
    {
        return cropBounds.Left == 0
            && cropBounds.Top == 0
            && cropBounds.Width == sourceWidth
            && cropBounds.Height == sourceHeight;
    }
}

public sealed record PreviewFrameCompositionRequest(
    byte[] SourcePixels,
    int SourceWidth,
    int SourceHeight,
    PreviewFrameCropSettings Crop);

public sealed record PreviewFrameCompositionResult(
    byte[] Pixels,
    int Width,
    int Height,
    PreviewCropBounds CropBounds,
    bool IsCropped);

public sealed record PreviewFrameCropSettings(
    bool IsEnabled,
    PreviewCropMode Mode,
    PreviewAbsoluteCrop Absolute,
    PreviewRelativeCrop Relative);

public sealed record PreviewAbsoluteCrop(
    int Left,
    int Top,
    int Width,
    int Height);

public sealed record PreviewRelativeCrop(
    int Left,
    int Top,
    int Right,
    int Bottom);

public sealed record PreviewCropBounds(
    int Left,
    int Top,
    int Width,
    int Height,
    int Right,
    int Bottom);

public enum PreviewCropMode
{
    Absolute,
    Relative
}
