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
        var tempDir = Path.Combine(Path.GetTempPath(), "AtlasForge_RoundtripTest_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Generate 3 dummy PNG files with different dimensions and transparent margins
            var frameFiles = new List<string>();
            var sizes = new[] { (40, 40), (50, 60), (30, 45) };
            for (var i = 0; i < sizes.Length; i++)
            {
                var (w, h) = sizes[i];
                var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(bmp))
                {
                    canvas.Clear(SKColors.Transparent);
                    // Draw a solid color rect in the center (leave 2px transparent margin)
                    canvas.DrawRect(SKRect.Create(2, 2, w - 4, h - 4), new SKPaint { Color = SKColors.Red });
                }
                var p = Path.Combine(tempDir, $"dummy_{i}.png");
                using var image = SKImage.FromBitmap(bmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(p);
                data.SaveTo(stream);
                frameFiles.Add(p);
                bmp.Dispose();
            }

            var imageProcessor = new ImageProcessingService();
            var binPacker = new BinPackingService();
            var exporter = new ExportService();
            var unpacker = new UnpackService();

            var frames = frameFiles.Select(imageProcessor.LoadFrame).ToList();

            // Pack settings
            var outDir = Path.Combine(tempDir, "output");
            var settings = new ExportSettings
            {
                PackingMode = PackingMode.BinPack,
                AlphaTrim = true,
                Padding = 2,
                MaxAtlasSize = 1024,
                ExportPng = true,
                ExportJson = true,
                ExportPlist = true,
                OutputPath = outDir
            };

            var normalized = imageProcessor.NormalizeFrames(frames, settings.AlphaTrim, settings.Padding);
            var atlasData = binPacker.Pack(normalized, settings);

            exporter.Export(atlasData, settings, "roundtrip");

            var plistPath = Path.Combine(outDir, "roundtrip.plist");
            var jsonPath = Path.Combine(outDir, "roundtrip.json");
            var pngPath = Path.Combine(outDir, "roundtrip.png");

            Assert.True(File.Exists(plistPath));
            Assert.True(File.Exists(jsonPath));
            Assert.True(File.Exists(pngPath));

            // Load packed image to simulate UI WriteableBitmap
            using var fileStream = File.OpenRead(pngPath);
            using var skiaBitmap = SKBitmap.Decode(fileStream);
            Assert.NotNull(skiaBitmap);

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

            // Unpack Plist
            var plistOutDir = Path.Combine(outDir, "plist_unpacked");
            var plistSlices = unpacker.ParsePlist(plistPath, writeableBitmap);
            unpacker.SaveSlices(pngPath, plistSlices, plistOutDir);

            // Unpack JSON
            var jsonOutDir = Path.Combine(outDir, "json_unpacked");
            var jsonSlices = unpacker.ParseJson(jsonPath, writeableBitmap);
            unpacker.SaveSlices(pngPath, jsonSlices, jsonOutDir);

            // Verify size matches original
            for (var i = 0; i < sizes.Length; i++)
            {
                var plistFile = Path.Combine(plistOutDir, $"dummy_{i}.png");
                var jsonFile = Path.Combine(jsonOutDir, $"dummy_{i}.png");

                Assert.True(File.Exists(plistFile));
                Assert.True(File.Exists(jsonFile));

                using var pStream = File.OpenRead(plistFile);
                using var pBmp = SKBitmap.Decode(pStream);
                Assert.Equal(sizes[i].Item1, pBmp.Width);
                Assert.Equal(sizes[i].Item2, pBmp.Height);

                using var jStream = File.OpenRead(jsonFile);
                using var jBmp = SKBitmap.Decode(jStream);
                Assert.Equal(sizes[i].Item1, jBmp.Width);
                Assert.Equal(sizes[i].Item2, jBmp.Height);
            }

            // Dispose frame SKBitmaps
            foreach (var f in frames)
            {
                f.Bitmap.Dispose();
            }
            foreach (var f in normalized)
            {
                f.Bitmap.Dispose();
            }
            atlasData.Atlas.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
