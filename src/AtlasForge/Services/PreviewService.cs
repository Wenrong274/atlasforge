using System.IO;
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
        var frameBitmap = new SKBitmap(frame.Width, frame.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(frameBitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(
            atlasData.Atlas,
            SKRect.Create(frame.X, frame.Y, frame.Width, frame.Height),
            SKRect.Create(0, 0, frame.Width, frame.Height));

        return BitmapFromSKBitmap(frameBitmap);
    }

    private static BitmapSource BitmapFromSKBitmap(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = stream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }
}
