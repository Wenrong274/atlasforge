using AtlasForge.Models;

using SkiaSharp;

namespace AtlasForge.Tests.Models;

public class DomainModelTests
{
    [Fact]
    public void ExportSettings_DefaultsMatchApplicationDefaults()
    {
        var settings = new ExportSettings();

        Assert.True(settings.ExportPng);
        Assert.False(settings.ExportJson);
        Assert.False(settings.ExportPlist);
        Assert.Equal("", settings.OutputPath);
        Assert.Equal(PackingMode.Grid, settings.PackingMode);
        Assert.True(settings.AutoGrid);
        Assert.Equal(4, settings.GridColumns);
        Assert.Equal(4, settings.GridRows);
        Assert.True(settings.AlphaTrim);
        Assert.Equal(2, settings.Padding);
        Assert.Equal(2048, settings.MaxAtlasSize);
    }

    [Fact]
    public void AtlasData_PreservesGridMetadataAndFrames()
    {
        using var bitmap = new SKBitmap(16, 16);
        var frames = new List<SpriteRect>
        {
            new("fire_001", 0, 0, 8, 8, 1, 2, 10, 12)
        };

        var atlas = new AtlasData(bitmap, 2, 3, 8, 8, frames, PackingMode.Grid);

        Assert.Same(bitmap, atlas.Atlas);
        Assert.Equal(2, atlas.Columns);
        Assert.Equal(3, atlas.Rows);
        Assert.Equal(8, atlas.FrameWidth);
        Assert.Equal(8, atlas.FrameHeight);
        Assert.Equal(frames, atlas.Frames);
        Assert.Equal(PackingMode.Grid, atlas.Mode);
    }
}