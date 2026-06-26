using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AtlasForge.Models;

using SkiaSharp;

namespace AtlasForge.Services;

public class PreviewService
{
    public BitmapSource RenderAtlas(AtlasData atlasData) =>
        BitmapFromSKBitmap(atlasData.Atlas);

    public BitmapSource RenderFrame(AtlasData atlasData, int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= atlasData.Frames.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex));
        }

        var frame = atlasData.Frames[frameIndex];
        using var frameBitmap = new SKBitmap(frame.SourceWidth, frame.SourceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(frameBitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(
            atlasData.Atlas,
            SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height),
            SKRect.Create(frame.OffsetX, frame.OffsetY, frame.Width, frame.Height));

        return BitmapFromSKBitmap(frameBitmap);
    }

    private static BitmapSource BitmapFromSKBitmap(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // Ensure we work with Bgra8888 for direct compatibility with WPF's PixelFormats.Bgra32
        using var bgraBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bgraBitmap))
        {
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        var writeableBitmap = new WriteableBitmap(
            width,
            height,
            96.0, // Force 96 DPI
            96.0, // Force 96 DPI
            PixelFormats.Bgra32,
            null
        );

        writeableBitmap.WritePixels(
            new Int32Rect(0, 0, width, height),
            bgraBitmap.GetPixels(),
            bgraBitmap.RowBytes * height,
            bgraBitmap.RowBytes
        );

        writeableBitmap.Freeze();
        return writeableBitmap;
    }
}
