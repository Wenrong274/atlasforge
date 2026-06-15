using AtlasForge.Services;
using SkiaSharp;

namespace AtlasForge.Tests.Services;

public class ImageProcessingServiceTests : IDisposable
{
    private readonly ImageProcessingService _svc = new();
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ImageProcessingServiceTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose()
    {
        Directory.Delete(_tmpDir, true);
        GC.SuppressFinalize(this);
    }

    private string MakePng(int width, int height, Action<SKCanvas> draw)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        draw(canvas);

        var path = Path.Combine(_tmpDir, $"{Guid.NewGuid()}.png");
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);

        return path;
    }

    [Fact]
    public void LoadFrame_ReturnsBitmapWithCorrectDimensions()
    {
        var path = MakePng(64, 64, canvas =>
            canvas.DrawRect(SKRect.Create(0, 0, 64, 64), new SKPaint { Color = SKColors.Red }));

        var frame = _svc.LoadFrame(path);

        Assert.Equal(64, frame.Bitmap.Width);
        Assert.Equal(64, frame.Bitmap.Height);
        Assert.Equal(64, frame.OriginalWidth);
        Assert.Equal(64, frame.OriginalHeight);
    }

    [Fact]
    public void LoadFrame_SetsFilePath()
    {
        var path = MakePng(32, 32, canvas =>
            canvas.DrawRect(SKRect.Create(0, 0, 32, 32), new SKPaint { Color = SKColors.Blue }));

        var frame = _svc.LoadFrame(path);

        Assert.Equal(path, frame.FilePath);
    }

    [Fact]
    public void ComputeTrimRect_FullyOpaqueImage_ReturnsFullRect()
    {
        var bitmap = new SKBitmap(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.DrawRect(SKRect.Create(0, 0, 32, 32), new SKPaint { Color = SKColors.Red });

        var rect = _svc.ComputeTrimRect(bitmap);

        Assert.Equal(new SKRectI(0, 0, 32, 32), rect);
    }

    [Fact]
    public void ComputeTrimRect_SpriteInCenter_ReturnsTightRect()
    {
        var bitmap = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawRect(SKRect.Create(10, 10, 20, 20), new SKPaint { Color = SKColors.Red });

        var rect = _svc.ComputeTrimRect(bitmap);

        Assert.Equal(10, rect.Left);
        Assert.Equal(10, rect.Top);
        Assert.Equal(30, rect.Right);
        Assert.Equal(30, rect.Bottom);
    }

    [Fact]
    public void ComputeTrimRect_FullyTransparent_ReturnsFullRect()
    {
        var bitmap = new SKBitmap(32, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var rect = _svc.ComputeTrimRect(bitmap);

        Assert.Equal(new SKRectI(0, 0, 32, 32), rect);
    }
}
