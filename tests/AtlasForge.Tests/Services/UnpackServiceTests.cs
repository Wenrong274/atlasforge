using System.Windows;
using System.Windows.Media.Imaging;
using AtlasForge.Models;
using AtlasForge.Services;
using SkiaSharp;

namespace AtlasForge.Tests.Services;

public class UnpackServiceTests
{
    [Fact]
    public void GenerateGridSlices_ReturnsCorrectCounts()
    {
        var service = new UnpackService();
        // 用無圖的 Null source 測試計算
        var slices = service.GenerateGridSlices(200, 200, 4, 4, 0, null!);
        Assert.Equal(16, slices.Count);
        Assert.Equal(50, slices[0].Width);
        Assert.Equal(50, slices[0].Height);
        Assert.Equal(50, slices[5].X); // 第二列第二行應在 (50, 50)
    }

    [Fact]
    public void GenerateGridSlices_WithPadding_ReturnsCorrectSizeAndCoordinates()
    {
        var service = new UnpackService();
        // 200x200, 4x4 grid -> cell size is 50x50.
        // padding = 2 -> trimmed size is 46x46, starting at (2, 2) offset in each cell.
        var slices = service.GenerateGridSlices(200, 200, 4, 4, 2, null!);
        Assert.Equal(16, slices.Count);
        Assert.Equal(46, slices[0].Width);
        Assert.Equal(46, slices[0].Height);
        Assert.Equal(2, slices[0].X);
        Assert.Equal(2, slices[0].Y);
        Assert.Equal(52, slices[5].X); // cell (1, 1) starts at 50, offset by 2 -> 52
        Assert.Equal(52, slices[5].Y);
    }

    [Fact]
    public void TestRoundtripPackAndUnpack()
    {
        var sourceDir = @"D:\Dev\新增資料夾\Comp 1";
        if (!Directory.Exists(sourceDir))
        {
            return; // skip if directory doesn't exist (e.g. on build server)
        }

        var files = Directory.GetFiles(sourceDir, "boom_*.png")
            .OrderBy(f => f, new NaturalSortComparer())
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        var imageProcessor = new ImageProcessingService();
        var binPacker = new BinPackingService();
        var exporter = new ExportService();
        var unpacker = new UnpackService();

        var frames = files.Select(imageProcessor.LoadFrame).ToList();

        // 1. Pack with AlphaTrim = true, Padding = 2
        var settings = new ExportSettings
        {
            PackingMode = PackingMode.BinPack,
            AlphaTrim = true,
            Padding = 2,
            MaxAtlasSize = 8192,
            ExportPng = true,
            ExportJson = true,
            ExportPlist = true,
            OutputPath = Path.Combine(Path.GetTempPath(), "AtlasForgeTestOut")
        };

        var normalized = imageProcessor.NormalizeFrames(frames, settings.AlphaTrim, settings.Padding);
        var atlasData = binPacker.Pack(normalized, settings);

        // Export
        var tempDir = settings.OutputPath;
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
        exporter.Export(atlasData, settings, "roundtrip_test");

        var plistPath = Path.Combine(tempDir, "roundtrip_test.plist");
        var jsonPath = Path.Combine(tempDir, "roundtrip_test.json");
        var pngPath = Path.Combine(tempDir, "roundtrip_test.png");

        Assert.True(File.Exists(plistPath));
        Assert.True(File.Exists(jsonPath));
        Assert.True(File.Exists(pngPath));

        // Load the packed preview image as 96 DPI
        using var fileStream = File.OpenRead(pngPath);
        using var skiaBitmap = SKBitmap.Decode(fileStream);
        Assert.NotNull(skiaBitmap);

        // Convert to 96 DPI BitmapSource just like the UI does
        var width = skiaBitmap.Width;
        var height = skiaBitmap.Height;
        using var bgraBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bgraBitmap))
        {
            canvas.DrawBitmap(skiaBitmap, 0, 0);
        }
        var writeableBitmap = new WriteableBitmap(
            width,
            height,
            96.0,
            96.0,
            System.Windows.Media.PixelFormats.Bgra32,
            null
        );
        writeableBitmap.WritePixels(
            new Int32Rect(0, 0, width, height),
            bgraBitmap.GetPixels(),
            bgraBitmap.RowBytes * height,
            bgraBitmap.RowBytes
        );
        writeableBitmap.Freeze();

        // 2. Unpack using Plist
        var plistOutDir = Path.Combine(tempDir, "plist_unpacked");
        var plistSlices = unpacker.ParsePlist(plistPath, writeableBitmap);
        unpacker.SaveSlices(pngPath, plistSlices, plistOutDir);

        // 3. Unpack using JSON
        var jsonOutDir = Path.Combine(tempDir, "json_unpacked");
        var jsonSlices = unpacker.ParseJson(jsonPath, writeableBitmap);
        unpacker.SaveSlices(pngPath, jsonSlices, jsonOutDir);

        // 4. Verify each unpacked file matches the original size
        foreach (var originalFrame in frames)
        {
            var name = Path.GetFileNameWithoutExtension(originalFrame.FilePath);
            var plistUnpackedFile = Path.Combine(plistOutDir, $"{name}.png");
            var jsonUnpackedFile = Path.Combine(jsonOutDir, $"{name}.png");

            Assert.True(File.Exists(plistUnpackedFile));
            Assert.True(File.Exists(jsonUnpackedFile));

            using var origStream = File.OpenRead(originalFrame.FilePath);
            using var origBmp = SKBitmap.Decode(origStream);

            using var plistStream = File.OpenRead(plistUnpackedFile);
            using var plistBmp = SKBitmap.Decode(plistStream);

            using var jsonStream = File.OpenRead(jsonUnpackedFile);
            using var jsonBmp = SKBitmap.Decode(jsonStream);

            Assert.Equal(origBmp.Width, plistBmp.Width);
            Assert.Equal(origBmp.Height, plistBmp.Height);
            Assert.Equal(origBmp.Width, jsonBmp.Width);
            Assert.Equal(origBmp.Height, jsonBmp.Height);
        }

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
