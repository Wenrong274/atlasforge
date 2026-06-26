using AtlasForge.Models;
using AtlasForge.Services;

using SkiaSharp;

namespace AtlasForge.Tests.Services;

public class BinPackingServiceTests
{
    private readonly BinPackingService _svc = new();

    private static List<FrameData> MakeFrames(int count, int width = 32, int height = 32)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using var canvas = new SKCanvas(bitmap);
                canvas.DrawRect(
                    SKRect.Create(0, 0, width, height),
                    new SKPaint { Color = SKColors.Red });

                return new FrameData(
                    $"frame_{index:D3}.png",
                    bitmap,
                    new SKRectI(0, 0, width, height),
                    width,
                    height);
            })
            .ToList();
    }

    [Fact]
    public void Pack_AllFramesPlaced()
    {
        var frames = MakeFrames(8, 32, 32);
        var settings = new ExportSettings { MaxAtlasSize = 2048 };

        var atlas = _svc.Pack(frames, settings);

        Assert.Equal(8, atlas.Frames.Count);
    }

    [Fact]
    public void Pack_NoFramesOverlap()
    {
        var frames = MakeFrames(12, 32, 32);
        var settings = new ExportSettings { MaxAtlasSize = 2048 };

        var atlas = _svc.Pack(frames, settings);

        for (var i = 0; i < atlas.Frames.Count; i++)
        {
            for (var j = i + 1; j < atlas.Frames.Count; j++)
            {
                var a = atlas.Frames[i];
                var b = atlas.Frames[j];
                var overlaps = a.X < b.X + b.Width && a.X + a.Width > b.X &&
                               a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

                Assert.False(overlaps, $"Frame {i} overlaps frame {j}");
            }
        }
    }

    [Fact]
    public void Pack_AtlasIsPoT()
    {
        var frames = MakeFrames(6, 32, 32);
        var settings = new ExportSettings { MaxAtlasSize = 2048 };

        var atlas = _svc.Pack(frames, settings);

        static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
        Assert.True(IsPowerOfTwo(atlas.Atlas.Width), $"Width {atlas.Atlas.Width} is not PoT");
        Assert.True(IsPowerOfTwo(atlas.Atlas.Height), $"Height {atlas.Atlas.Height} is not PoT");
    }

    [Fact]
    public void Pack_ModeIsBinPack()
    {
        var frames = MakeFrames(4, 32, 32);
        var settings = new ExportSettings { MaxAtlasSize = 2048 };

        var atlas = _svc.Pack(frames, settings);

        Assert.Equal(PackingMode.BinPack, atlas.Mode);
    }

    [Fact]
    public void Pack_EmptyFrames_Throws()
    {
        var settings = new ExportSettings();

        Assert.Throws<ArgumentException>(() => _svc.Pack([], settings));
    }
}
