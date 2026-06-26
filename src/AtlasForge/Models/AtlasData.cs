using SkiaSharp;

namespace AtlasForge.Models;

public record AtlasData(
    SKBitmap Atlas,
    int Columns,
    int Rows,
    int FrameWidth,
    int FrameHeight,
    List<SpriteRect> Frames,
    PackingMode Mode);
