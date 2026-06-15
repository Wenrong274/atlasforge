using AtlasForge.Models;
using AtlasForge.Services;
using SkiaSharp;

namespace AtlasForge.Tests.Services;

public class GridPackingServiceTests
{
    private readonly GridPackingService _svc = new();

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

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(9, 3, 3)]
    [InlineData(6, 3, 2)]
    [InlineData(24, 5, 5)]
    public void AutoGrid_ReturnsMinimalGrid(int frameCount, int expectedColumns, int expectedRows)
    {
        var (columns, rows) = _svc.AutoGrid(frameCount);

        Assert.Equal(expectedColumns, columns);
        Assert.Equal(expectedRows, rows);
        Assert.True(columns * rows >= frameCount);
    }

    [Fact]
    public void Pack_AutoGrid_AtlasSizeEqualsColsTimesFrameSize()
    {
        var frames = MakeFrames(6, 32, 32);
        var settings = new ExportSettings { AutoGrid = true, AlphaTrim = false, Padding = 0 };

        var atlas = _svc.Pack(frames, settings);

        var (columns, rows) = _svc.AutoGrid(6);
        Assert.Equal(columns * 32, atlas.Atlas.Width);
        Assert.Equal(rows * 32, atlas.Atlas.Height);
        Assert.Equal(PackingMode.Grid, atlas.Mode);
    }

    [Fact]
    public void Pack_ManualGrid_UsesSpecifiedColsRows()
    {
        var frames = MakeFrames(6, 32, 32);
        var settings = new ExportSettings
        {
            AutoGrid = false,
            GridColumns = 3,
            GridRows = 2,
            AlphaTrim = false,
            Padding = 0
        };

        var atlas = _svc.Pack(frames, settings);

        Assert.Equal(96, atlas.Atlas.Width);
        Assert.Equal(64, atlas.Atlas.Height);
    }

    [Fact]
    public void Pack_SpriteRectsHaveCorrectPositions()
    {
        var frames = MakeFrames(4, 32, 32);
        var settings = new ExportSettings
        {
            AutoGrid = false,
            GridColumns = 2,
            GridRows = 2,
            AlphaTrim = false,
            Padding = 0
        };

        var atlas = _svc.Pack(frames, settings);

        Assert.Equal(new SpriteRect("frame_000", 0, 0, 32, 32, 0, 0, 32, 32), atlas.Frames[0]);
        Assert.Equal(new SpriteRect("frame_001", 32, 0, 32, 32, 0, 0, 32, 32), atlas.Frames[1]);
        Assert.Equal(new SpriteRect("frame_002", 0, 32, 32, 32, 0, 0, 32, 32), atlas.Frames[2]);
        Assert.Equal(new SpriteRect("frame_003", 32, 32, 32, 32, 0, 0, 32, 32), atlas.Frames[3]);
    }

    [Fact]
    public void Pack_EmptyFrames_Throws()
    {
        var settings = new ExportSettings();

        Assert.Throws<ArgumentException>(() => _svc.Pack([], settings));
    }
}
