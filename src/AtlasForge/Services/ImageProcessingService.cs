using AtlasForge.Models;
using SkiaSharp;

namespace AtlasForge.Services;

public class ImageProcessingService
{
    public FrameData LoadFrame(string filePath)
    {
        var bitmap = SKBitmap.Decode(filePath)
            ?? throw new InvalidOperationException($"Cannot decode: {filePath}");
        var trimRect = ComputeTrimRect(bitmap);

        return new FrameData(filePath, bitmap, trimRect, bitmap.Width, bitmap.Height);
    }

    public SKRectI ComputeTrimRect(SKBitmap bitmap)
    {
        var left = bitmap.Width;
        var right = -1;
        var top = bitmap.Height;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0
            ? new SKRectI(0, 0, bitmap.Width, bitmap.Height)
            : new SKRectI(left, top, right + 1, bottom + 1);
    }

    public List<FrameData> NormalizeFrames(List<FrameData> frames, bool alphaTrim, int padding)
    {
        if (frames.Count == 0)
        {
            return [];
        }

        var sourceRects = frames
            .Select(frame => alphaTrim
                ? frame.TrimRect
                : new SKRectI(0, 0, frame.Bitmap.Width, frame.Bitmap.Height))
            .ToList();

        var maxWidth = sourceRects.Max(rect => rect.Width);
        var maxHeight = sourceRects.Max(rect => rect.Height);
        var cellWidth = maxWidth + padding * 2;
        var cellHeight = maxHeight + padding * 2;

        return frames
            .Select((frame, index) =>
            {
                var sourceRect = sourceRects[index];
                var drawX = padding + (maxWidth - sourceRect.Width) / 2;
                var drawY = padding + (maxHeight - sourceRect.Height) / 2;

                return CreateNormalized(frame, sourceRect, cellWidth, cellHeight, drawX, drawY);
            })
            .ToList();
    }

    private static FrameData CreateNormalized(
        FrameData frame,
        SKRectI sourceRect,
        int cellWidth,
        int cellHeight,
        int drawX,
        int drawY)
    {
        var normalized = new SKBitmap(cellWidth, cellHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(normalized);
        canvas.Clear(SKColors.Transparent);

        var source = SKRect.Create(sourceRect.Left, sourceRect.Top, sourceRect.Width, sourceRect.Height);
        var destination = SKRect.Create(drawX, drawY, sourceRect.Width, sourceRect.Height);
        canvas.DrawBitmap(frame.Bitmap, source, destination);

        return frame with
        {
            Bitmap = normalized,
            TrimRect = new SKRectI(0, 0, cellWidth, cellHeight)
        };
    }
}
