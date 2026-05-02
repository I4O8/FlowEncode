using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FlowEncode;

internal sealed class PreviewBitmapSurface
{
    private WriteableBitmap? _bitmap;

    public async Task<WriteableBitmap> UpdateAsync(ReadOnlyMemory<byte> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var requiredPixelLength = checked(width * height * 4);
        if (pixels.Length < requiredPixelLength)
        {
            throw new ArgumentException("Pixel buffer is shorter than the required frame size.", nameof(pixels));
        }

        if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height);
        }

        using var stream = _bitmap.PixelBuffer.AsStream();
        stream.Position = 0;
        await stream.WriteAsync(pixels[..requiredPixelLength]);
        await stream.FlushAsync();
        _bitmap.Invalidate();
        return _bitmap;
    }

    public void Reset()
    {
        _bitmap = null;
    }
}
