using System.Windows.Media.Imaging;
using SkiaSharp;

namespace AtlasForge.Tests;

public class UnitTest1
{
    [Fact]
    public void TestSKBitmapDpi()
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

        // Print details or assert
        Assert.Equal(100, bitmapImage.PixelWidth);
        Assert.Equal(100, bitmapImage.PixelHeight);
        Assert.Equal(96.0, bitmapImage.DpiX);
        Assert.Equal(96.0, bitmapImage.DpiY);
        Assert.Equal(100.0, bitmapImage.Width);
        Assert.Equal(100.0, bitmapImage.Height);
    }

    [Fact]
    public void TestRealBoomPng()
    {
        var path = @"D:\Dev\新增資料夾\Comp 1\新增資料夾\boom.png";
        if (File.Exists(path))
        {
            using var fileStream = File.OpenRead(path);
            using var skiaBitmap = SKBitmap.Decode(fileStream);
            Assert.NotNull(skiaBitmap);

            using var image = SKImage.FromBitmap(skiaBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = stream;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            // Check if DPI is indeed 96 or something else
            Assert.True(bitmapImage.DpiX > 0);
        }
    }
}
