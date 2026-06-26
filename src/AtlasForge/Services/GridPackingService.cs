using System.IO;

using AtlasForge.Models;

using SkiaSharp;

namespace AtlasForge.Services;

public class GridPackingService
{
    public (int columns, int rows) AutoGrid(int frameCount)
    {
        if (frameCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "幀數量必須大於零 (Frame count must be greater than zero).");
        }

        var columns = (int)Math.Ceiling(Math.Sqrt(frameCount));
        var rows = (int)Math.Ceiling((double)frameCount / columns);

        return (columns, rows);
    }

    public AtlasData Pack(List<FrameData> frames, ExportSettings settings)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("沒有可封裝的幀 (No frames to pack).", nameof(frames));
        }

        var (columns, rows) = settings.AutoGrid
            ? AutoGrid(frames.Count)
            : (settings.GridColumns, settings.GridRows);

        if (columns <= 0 || rows <= 0 || columns * rows < frames.Count)
        {
            throw new ArgumentException(
                $"網格 {columns}x{rows}（{columns * rows} 格）裝不下 {frames.Count} 個幀 (Grid cannot hold all frames)。請調整行/列數，或開啟「自動計算行列」(enable Auto Grid)。",
                nameof(settings));
        }

        var frameWidth = frames[0].Bitmap.Width;
        var frameHeight = frames[0].Bitmap.Height;
        var atlasBitmap = new SKBitmap(
            columns * frameWidth,
            rows * frameHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var canvas = new SKCanvas(atlasBitmap);
        canvas.Clear(SKColors.Transparent);

        var spriteRects = new List<SpriteRect>();
        for (var index = 0; index < frames.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = column * frameWidth;
            var y = row * frameHeight;

            canvas.DrawBitmap(frames[index].Bitmap, x, y);
            spriteRects.Add(new SpriteRect(
                Path.GetFileNameWithoutExtension(frames[index].FilePath),
                x,
                y,
                frameWidth,
                frameHeight,
                0,
                0,
                frames[index].OriginalWidth,
                frames[index].OriginalHeight));
        }

        return new AtlasData(
            atlasBitmap,
            columns,
            rows,
            frameWidth,
            frameHeight,
            spriteRects,
            PackingMode.Grid);
    }
}
