using AtlasForge.Models;
using SkiaSharp;

namespace AtlasForge.Services;

public class ImageProcessingService
{
    public FrameData LoadFrame(string filePath)
    {
        var bitmap = SKBitmap.Decode(filePath)
            ?? throw new InvalidOperationException($"Cannot decode: {filePath}");
        var trimRect = ComputeTrimRect(bitmap);

        return new FrameData(filePath, bitmap, trimRect, bitmap.Width, bitmap.Height);
    }

    public SKRectI ComputeTrimRect(SKBitmap bitmap)
    {
        var left = bitmap.Width;
        var right = -1;
        var top = bitmap.Height;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0
            ? new SKRectI(0, 0, bitmap.Width, bitmap.Height)
            : new SKRectI(left, top, right + 1, bottom + 1);
    }
}
