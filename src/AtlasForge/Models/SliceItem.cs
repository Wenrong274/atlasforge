using System.Windows.Media;

namespace AtlasForge.Models;

public class SliceItem
{
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public bool Rotated { get; set; }
    public ImageSource? Thumbnail { get; set; }
}
