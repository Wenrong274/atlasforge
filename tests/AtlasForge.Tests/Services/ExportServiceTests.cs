using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using AtlasForge.Models;
using AtlasForge.Services;
using SkiaSharp;

namespace AtlasForge.Tests.Services;

public class ExportServiceTests : IDisposable
{
    private readonly ExportService _svc = new();
    private readonly string _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ExportServiceTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose()
    {
        Directory.Delete(_tmpDir, true);
        GC.SuppressFinalize(this);
    }

    private static AtlasData MakeAtlas()
    {
        var bitmap = new SKBitmap(64, 32, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.DrawRect(SKRect.Create(0, 0, 64, 32), new SKPaint { Color = SKColors.Red });
        var frames = new List<SpriteRect>
        {
            new("fire_001", 0, 0, 32, 32, 0, 0, 32, 32),
            new("fire_002", 32, 0, 32, 32, 0, 0, 32, 32)
        };

        return new AtlasData(bitmap, 2, 1, 32, 32, frames, PackingMode.Grid);
    }

    [Fact]
    public void ExportPng_CreatesFile()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings { OutputPath = _tmpDir, ExportPng = true };

        _svc.Export(atlas, settings, "fire");

        Assert.True(File.Exists(Path.Combine(_tmpDir, "fire.png")));
    }

    [Fact]
    public void ExportJson_CreatesFileWithCorrectFrames()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings { OutputPath = _tmpDir, ExportPng = false, ExportJson = true };

        _svc.Export(atlas, settings, "fire");

        var jsonPath = Path.Combine(_tmpDir, "fire.json");
        Assert.True(File.Exists(jsonPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var frames = doc.RootElement.GetProperty("frames");
        Assert.True(frames.TryGetProperty("fire_001", out var frame));
        Assert.Equal(0, frame.GetProperty("frame").GetProperty("x").GetInt32());
        Assert.Equal(0, frame.GetProperty("frame").GetProperty("y").GetInt32());
        Assert.Equal(32, frame.GetProperty("frame").GetProperty("w").GetInt32());
        Assert.Equal(32, frame.GetProperty("frame").GetProperty("h").GetInt32());
    }

    [Fact]
    public void ExportJson_MetaHasCorrectImageAndSize()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings { OutputPath = _tmpDir, ExportPng = false, ExportJson = true };

        _svc.Export(atlas, settings, "fire");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tmpDir, "fire.json")));
        var meta = doc.RootElement.GetProperty("meta");
        Assert.Equal("fire.png", meta.GetProperty("image").GetString());
        Assert.Equal(64, meta.GetProperty("size").GetProperty("w").GetInt32());
    }

    [Fact]
    public void ExportJson_MetaVersionUsesAssemblyInformationalVersion()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings { OutputPath = _tmpDir, ExportPng = false, ExportJson = true };
        var expectedVersion = typeof(ExportService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        _svc.Export(atlas, settings, "fire");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_tmpDir, "fire.json")));
        var meta = doc.RootElement.GetProperty("meta");
        Assert.Equal(expectedVersion, meta.GetProperty("version").GetString());
    }

    [Fact]
    public void ExportPlist_CreatesFileWithFrameKey()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings { OutputPath = _tmpDir, ExportPng = false, ExportPlist = true };

        _svc.Export(atlas, settings, "fire");

        var plistPath = Path.Combine(_tmpDir, "fire.plist");
        Assert.True(File.Exists(plistPath));

        var xml = XDocument.Load(plistPath);
        var content = xml.ToString();
        Assert.Contains("fire_001.png", content);
        Assert.Contains("{0,0}", content);
    }

    [Fact]
    public void Export_OnlyCheckedFormats_OnlyThoseFilesCreated()
    {
        var atlas = MakeAtlas();
        var settings = new ExportSettings
        {
            OutputPath = _tmpDir,
            ExportPng = true,
            ExportJson = false,
            ExportPlist = false
        };

        _svc.Export(atlas, settings, "fire");

        Assert.True(File.Exists(Path.Combine(_tmpDir, "fire.png")));
        Assert.False(File.Exists(Path.Combine(_tmpDir, "fire.json")));
        Assert.False(File.Exists(Path.Combine(_tmpDir, "fire.plist")));
    }
}
