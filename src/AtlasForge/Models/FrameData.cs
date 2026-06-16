using SkiaSharp;

namespace AtlasForge.Models;

public record FrameData(
    string FilePath,
    SKBitmap Bitmap,
    SKRectI TrimRect,
    int OriginalWidth,
    int OriginalHeight);