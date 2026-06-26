using System.IO;

using AtlasForge.Models;

using SkiaSharp;

namespace AtlasForge.Services;

public class BinPackingService
{
    private readonly record struct Rect(int X, int Y, int Width, int Height)
    {
        public int Right => X + Width;
        public int Bottom => Y + Height;
    }

    private readonly record struct Placement(FrameData Frame, int X, int Y, int Width, int Height);

    public AtlasData Pack(List<FrameData> frames, ExportSettings settings)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("沒有可封裝的幀 (No frames to pack).", nameof(frames));
        }

        var maxSize = settings.MaxAtlasSize;
        var freeRects = new List<Rect> { new(0, 0, maxSize, maxSize) };
        var placements = new Dictionary<FrameData, Placement>();

        foreach (var frame in frames.OrderByDescending(frame => frame.Bitmap.Width * frame.Bitmap.Height))
        {
            var width = frame.Bitmap.Width;
            var height = frame.Bitmap.Height;
            var bestIndex = FindBestFreeRect(freeRects, width, height);

            if (bestIndex < 0)
            {
                if (width > maxSize || height > maxSize)
                {
                    throw new InvalidOperationException(
                        $"幀尺寸 {width}x{height} 大於 Atlas 上限 {maxSize}x{maxSize} (Frame larger than atlas)：{frame.FilePath}。請調高「最大 Atlas 尺寸」(Increase Max Atlas Size)。");
                }

                throw new InvalidOperationException(
                    $"Atlas 空間不足 (Ran out of space in {maxSize}x{maxSize} atlas)：{frame.FilePath}。請調高「最大 Atlas 尺寸」或減少幀數 (Increase Max Atlas Size or remove frames)。");
            }

            var best = freeRects[bestIndex];
            var used = new Rect(best.X, best.Y, width, height);
            placements[frame] = new Placement(frame, best.X, best.Y, width, height);

            SplitFreeRects(freeRects, used);
            PruneContainedFreeRects(freeRects);
        }

        var usedWidth = placements.Values.Max(placement => placement.X + placement.Width);
        var usedHeight = placements.Values.Max(placement => placement.Y + placement.Height);
        var atlasWidth = NextPowerOfTwo(usedWidth);
        var atlasHeight = NextPowerOfTwo(usedHeight);
        var atlasBitmap = new SKBitmap(atlasWidth, atlasHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(atlasBitmap);
        canvas.Clear(SKColors.Transparent);

        var spriteRects = new List<SpriteRect>();
        foreach (var frame in frames)
        {
            var placement = placements[frame];
            canvas.DrawBitmap(frame.Bitmap, placement.X, placement.Y);
            spriteRects.Add(new SpriteRect(
                Path.GetFileNameWithoutExtension(frame.FilePath),
                placement.X + frame.PackedOffsetX,
                placement.Y + frame.PackedOffsetY,
                frame.TrimRect.Width,
                frame.TrimRect.Height,
                frame.TrimRect.Left,
                frame.TrimRect.Top,
                frame.OriginalWidth,
                frame.OriginalHeight));
        }

        return new AtlasData(atlasBitmap, 0, 0, 0, 0, spriteRects, PackingMode.BinPack);
    }

    private static int FindBestFreeRect(List<Rect> freeRects, int width, int height)
    {
        var bestIndex = -1;
        var bestShortSide = int.MaxValue;
        var bestLongSide = int.MaxValue;

        for (var index = 0; index < freeRects.Count; index++)
        {
            var free = freeRects[index];
            if (free.Width < width || free.Height < height)
            {
                continue;
            }

            var leftoverWidth = free.Width - width;
            var leftoverHeight = free.Height - height;
            var shortSide = Math.Min(leftoverWidth, leftoverHeight);
            var longSide = Math.Max(leftoverWidth, leftoverHeight);

            if (shortSide < bestShortSide ||
                shortSide == bestShortSide && longSide < bestLongSide)
            {
                bestIndex = index;
                bestShortSide = shortSide;
                bestLongSide = longSide;
            }
        }

        return bestIndex;
    }

    private static void SplitFreeRects(List<Rect> freeRects, Rect used)
    {
        for (var index = freeRects.Count - 1; index >= 0; index--)
        {
            var free = freeRects[index];
            if (!Overlaps(free, used))
            {
                continue;
            }

            freeRects.RemoveAt(index);

            if (used.X > free.X)
            {
                freeRects.Add(new Rect(free.X, free.Y, used.X - free.X, free.Height));
            }

            if (used.Right < free.Right)
            {
                freeRects.Add(new Rect(used.Right, free.Y, free.Right - used.Right, free.Height));
            }

            if (used.Y > free.Y)
            {
                freeRects.Add(new Rect(free.X, free.Y, free.Width, used.Y - free.Y));
            }

            if (used.Bottom < free.Bottom)
            {
                freeRects.Add(new Rect(free.X, used.Bottom, free.Width, free.Bottom - used.Bottom));
            }
        }
    }

    private static void PruneContainedFreeRects(List<Rect> freeRects)
    {
        for (var i = freeRects.Count - 1; i >= 0; i--)
        {
            for (var j = freeRects.Count - 1; j >= 0; j--)
            {
                if (i == j)
                {
                    continue;
                }

                if (Contains(freeRects[j], freeRects[i]))
                {
                    freeRects.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static bool Contains(Rect outer, Rect inner) =>
        outer.X <= inner.X &&
        outer.Y <= inner.Y &&
        outer.Right >= inner.Right &&
        outer.Bottom >= inner.Bottom;

    private static bool Overlaps(Rect a, Rect b) =>
        a.X < b.Right &&
        a.Right > b.X &&
        a.Y < b.Bottom &&
        a.Bottom > b.Y;

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1)
        {
            return 1;
        }

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
