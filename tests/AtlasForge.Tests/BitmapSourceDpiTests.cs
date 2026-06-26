using System.Windows.Media.Imaging;

using SkiaSharp;

namespace AtlasForge.Tests;

public class BitmapSourceDpiTests
{
    [Fact]
    public void PngDecodedByWpf_UsesExpectedPixelSizeAndDpi()
    {
        using var bitmap = new SKBitmap(100, 100, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = stream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        Assert.Equal(100, bitmapImage.PixelWidth);
        Assert.Equal(100, bitmapImage.PixelHeight);
        Assert.Equal(96.0, bitmapImage.DpiX);
        Assert.Equal(96.0, bitmapImage.DpiY);
        Assert.Equal(100.0, bitmapImage.Width);
        Assert.Equal(100.0, bitmapImage.Height);
    }
}
