using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using AtlasForge.Models;
using SkiaSharp;

namespace AtlasForge.Services;

public class UnpackService
{
    public List<SliceItem> ParseJson(string jsonPath, BitmapSource atlasSource)
    {
        var list = new List<SliceItem>();
        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var root = doc.RootElement;
        if (!root.TryGetProperty("frames", out var framesProp))
        {
            return list;
        }

        foreach (var frameObj in framesProp.EnumerateObject())
        {
            var name = frameObj.Name;
            var val = frameObj.Value;
            var frame = val.GetProperty("frame");
            var spriteSourceSize = val.GetProperty("spriteSourceSize");
            var sourceSize = val.GetProperty("sourceSize");

            var rotated = false;
            if (val.TryGetProperty("rotated", out var rotProp))
            {
                rotated = rotProp.GetBoolean();
            }

            var item = new SliceItem
            {
                Name = Path.GetFileNameWithoutExtension(name),
                X = frame.GetProperty("x").GetInt32(),
                Y = frame.GetProperty("y").GetInt32(),
                Width = spriteSourceSize.GetProperty("w").GetInt32(),
                Height = spriteSourceSize.GetProperty("h").GetInt32(),
                OffsetX = spriteSourceSize.GetProperty("x").GetInt32(),
                OffsetY = spriteSourceSize.GetProperty("y").GetInt32(),
                SourceWidth = sourceSize.GetProperty("w").GetInt32(),
                SourceHeight = sourceSize.GetProperty("h").GetInt32(),
                Rotated = rotated
            };

            item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height, item.Rotated);
            list.Add(item);
        }
        return list;
    }

    public List<SliceItem> ParsePlist(string plistPath, BitmapSource atlasSource)
    {
        var list = new List<SliceItem>();
        var doc = XDocument.Load(plistPath);
        var plistDict = doc.Element("plist")?.Element("dict");
        if (plistDict is null)
        {
            return list;
        }

        var framesKey = plistDict.Elements("key").FirstOrDefault(k => k.Value == "frames");
        if (framesKey?.NextNode is not XElement framesDict || framesDict.Name != "dict")
        {
            return list;
        }

        XElement? currentKey = null;
        foreach (var element in framesDict.Elements())
        {
            if (element.Name == "key")
            {
                currentKey = element;
            }
            else if (element.Name == "dict" && currentKey is not null)
            {
                var name = currentKey.Value;
                var frameDict = element;
                currentKey = null; // reset key

                var keysInFrame = frameDict.Elements("key").ToList();
                var frameStr = "";
                var offsetStr = "";
                var sourceSizeStr = "";
                var sourceColorRectStr = "";
                var rotated = false;

                for (var j = 0; j < keysInFrame.Count; j++)
                {
                    var k = keysInFrame[j].Value;
                    if (keysInFrame[j].NextNode is not XElement v)
                    {
                        continue;
                    }

                    if (k == "frame")
                    {
                        frameStr = v.Value;
                    }
                    else if (k == "offset")
                    {
                        offsetStr = v.Value;
                    }
                    else if (k == "sourceSize")
                    {
                        sourceSizeStr = v.Value;
                    }
                    else if (k == "sourceColorRect")
                    {
                        sourceColorRectStr = v.Value;
                    }
                    else if (k == "rotated")
                    {
                        rotated = v.Name.LocalName == "true";
                    }
                }

                var (fx, fy, fw, fh) = ParseRect(frameStr);
                var (sw, sh) = ParsePoint(sourceSizeStr);

                // 如果有旋轉，plist 裡面的寬高代表的是合圖上的尺寸（已旋轉）
                // 還原後原始被裁切的寬高為：Width = fh, Height = fw
                var originalTrimmedWidth = rotated ? fh : fw;
                var originalTrimmedHeight = rotated ? fw : fh;

                var leftTopX = 0;
                var leftTopY = 0;

                if (!string.IsNullOrEmpty(sourceColorRectStr))
                {
                    var (scX, scY, _, _) = ParseRect(sourceColorRectStr);
                    leftTopX = scX;
                    leftTopY = scY;
                }
                else if (!string.IsNullOrEmpty(offsetStr))
                {
                    var (ox, oy) = ParsePoint(offsetStr);
                    leftTopX = (sw - originalTrimmedWidth) / 2 + ox;
                    leftTopY = (sh - originalTrimmedHeight) / 2 - oy; // plist 通常 Y 軸向上
                }

                var item = new SliceItem
                {
                    Name = Path.GetFileNameWithoutExtension(name),
                    X = fx,
                    Y = fy,
                    Width = originalTrimmedWidth,
                    Height = originalTrimmedHeight,
                    OffsetX = Math.Max(0, leftTopX),
                    OffsetY = Math.Max(0, leftTopY),
                    SourceWidth = sw,
                    SourceHeight = sh,
                    Rotated = rotated
                };

                item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height, item.Rotated);
                list.Add(item);
            }
        }

        return list;
    }

    public List<SliceItem> GenerateGridSlices(int imgWidth, int imgHeight, int cols, int rows, int padding, BitmapSource atlasSource)
    {
        var list = new List<SliceItem>();
        if (cols <= 0 || rows <= 0)
        {
            return list;
        }

        var cellW = imgWidth / cols;
        var cellH = imgHeight / rows;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var index = r * cols + c;

                var x = c * cellW;
                var y = r * cellH;
                var w = cellW - padding * 2;
                var h = cellH - padding * 2;

                if (w <= 0 || h <= 0)
                {
                    continue; // 避免 padding 大於單格尺寸導致錯誤
                }

                var item = new SliceItem
                {
                    Name = $"frame_{index}",
                    X = x + padding,
                    Y = y + padding,
                    Width = w,
                    Height = h,
                    OffsetX = 0,
                    OffsetY = 0,
                    SourceWidth = w,
                    SourceHeight = h,
                    Rotated = false
                };
                item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height, item.Rotated);
                list.Add(item);
            }
        }
        return list;
    }

    public void SaveSlices(string atlasImagePath, List<SliceItem> slices, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        using var readStream = File.OpenRead(atlasImagePath);
        using var atlas = SKBitmap.Decode(readStream);
        if (atlas is null)
        {
            throw new InvalidOperationException("無法載入合圖點陣圖。");
        }

        foreach (var slice in slices)
        {
            using var restored = new SKBitmap(slice.SourceWidth, slice.SourceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(restored))
            {
                canvas.Clear(SKColors.Transparent);

                if (slice.Rotated)
                {
                    // 旋轉情況下，合圖中的實際寬高為 slice.Height (合圖寬) 與 slice.Width (合圖高)
                    using var rotatedBitmap = new SKBitmap(slice.Height, slice.Width, SKColorType.Rgba8888, SKAlphaType.Premul);
                    using (var tempCanvas = new SKCanvas(rotatedBitmap))
                    {
                        tempCanvas.Clear(SKColors.Transparent);
                        tempCanvas.DrawBitmap(
                            atlas,
                            SKRect.Create(slice.X, slice.Y, slice.Height, slice.Width),
                            SKRect.Create(0, 0, slice.Height, slice.Width));
                    }

                    // 順時針旋轉 90 度並繪製到 restored 畫布的偏移點
                    canvas.Save();
                    canvas.Translate(slice.OffsetX, slice.OffsetY);
                    canvas.RotateDegrees(90);
                    canvas.Translate(0, -slice.Width); // 旋轉後沿新局部 Y 軸偏移，對齊原本的左上角
                    canvas.DrawBitmap(rotatedBitmap, 0, 0);
                    canvas.Restore();
                }
                else
                {
                    var srcRect = SKRect.Create(slice.X, slice.Y, slice.Width, slice.Height);
                    var destRect = SKRect.Create(slice.OffsetX, slice.OffsetY, slice.Width, slice.Height);
                    canvas.DrawBitmap(atlas, srcRect, destRect);
                }
            }

            var outPath = Path.Combine(outputDir, $"{slice.Name}.png");
            using var image = SKImage.FromBitmap(restored);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(outPath);
            data.SaveTo(stream);
        }
    }

    private static BitmapSource CreateThumbnail(BitmapSource source, int x, int y, int w, int h, bool rotated = false)
    {
        if (source is null)
        {
            return null!;
        }

        // 旋轉時，合圖中裁切區域的實際寬高會互換
        var cropW = rotated ? h : w;
        var cropH = rotated ? w : h;

        if (x < 0 || y < 0 || x + cropW > source.PixelWidth || y + cropH > source.PixelHeight || cropW <= 0 || cropH <= 0)
        {
            return new CroppedBitmap(source, new Int32Rect(0, 0, 1, 1));
        }

        var cropped = new CroppedBitmap(source, new Int32Rect(x, y, cropW, cropH));
        if (!rotated)
        {
            return cropped;
        }

        // 順時針旋轉 90 度還原
        var transform = new TransformedBitmap();
        transform.BeginInit();
        transform.Source = cropped;
        transform.Transform = new RotateTransform(90);
        transform.EndInit();
        transform.Freeze();
        return transform;
    }

    private static (int X, int Y) ParsePoint(string s)
    {
        var parts = s.Trim('{', '}').Split(',');
        if (parts.Length < 2)
        {
            return (0, 0);
        }
        var x = (int)Math.Round(double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
        var y = (int)Math.Round(double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        return (x, y);
    }

    private static (int X, int Y, int W, int H) ParseRect(string s)
    {
        var cleaned = s.Replace(" ", "").Replace("{{", "").Replace("}}", "").Replace("{", "").Replace("}", "");
        var parts = cleaned.Split(',');
        if (parts.Length < 4)
        {
            return (0, 0, 0, 0);
        }
        var x = (int)Math.Round(double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
        var y = (int)Math.Round(double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
        var w = (int)Math.Round(double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        var h = (int)Math.Round(double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
        return (x, y, w, h);
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

    public (int cols, int rows, int padding) DetectGrid(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // 1. 統計每一列和每一行的非透明像素數
        var colHasAlpha = new bool[width];
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    colHasAlpha[x] = true;
                    break;
                }
            }
        }

        var rowHasAlpha = new bool[height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                {
                    rowHasAlpha[y] = true;
                    break;
                }
            }
        }

        // 2. 分析有色區段的區間
        var colRanges = GetActiveRanges(colHasAlpha);
        var rowRanges = GetActiveRanges(rowHasAlpha);

        if (colRanges.Count == 0 || rowRanges.Count == 0)
        {
            return (4, 4, 0); // 預設降級
        }

        // 3. 估算單格週期大小 (cellWidth & cellHeight)
        // 為了防止後面大尺寸子圖相連，我們利用最前面幾格的間隔來推算均勻週期 T。
        var cellWidth = EstimatePeriod(colRanges, width);
        var cellHeight = EstimatePeriod(rowRanges, height);

        var cols = (int)Math.Round((double)width / cellWidth);
        var rows = (int)Math.Round((double)height / cellHeight);

        if (cols <= 0)
        {
            cols = colRanges.Count;
        }

        if (rows <= 0)
        {
            rows = rowRanges.Count;
        }

        // 計算 padding
        // 最左邊有像素的起點是 colRanges[0].Start。
        // 最右邊有像素的終點是 colRanges[^1].End。
        var padLeft = colRanges[0].Start;
        var padRight = width - colRanges[^1].End;
        var padTop = rowRanges[0].Start;
        var padBottom = height - rowRanges[^1].End;

        // 最外邊緣透明寬度的最小值
        var padding = Math.Min(Math.Min(padLeft, padRight), Math.Min(padTop, padBottom));

        // 限制 padding 合理範圍
        if (padding < 0 || padding > 50)
        {
            padding = 0;
        }

        return (cols, rows, padding);
    }

    private static int EstimatePeriod(List<(int Start, int End)> ranges, int totalSize)
    {
        if (ranges.Count >= 2)
        {
            // 蒐集相鄰區間起點的差值
            var diffs = new List<int>();
            for (var i = 1; i < ranges.Count; i++)
            {
                var diff = ranges[i].Start - ranges[i - 1].Start;
                if (diff > 32) // 忽略太小的雜訊區間
                {
                    diffs.Add(diff);
                }
            }

            if (diffs.Count > 0)
            {
                // 取最小的合理週期作為 T（通常就是單一格子的寬度）
                var estimatedPeriod = diffs.Min();
                if (estimatedPeriod > 32 && estimatedPeriod < totalSize)
                {
                    return estimatedPeriod;
                }
            }
        }

        // 降級估計：直接平分
        return totalSize / ranges.Count;
    }

    private static List<(int Start, int End)> GetActiveRanges(bool[] hasAlpha)
    {
        var ranges = new List<(int Start, int End)>();
        var inRange = false;
        var start = 0;

        for (var i = 0; i < hasAlpha.Length; i++)
        {
            if (hasAlpha[i] && !inRange)
            {
                start = i;
                inRange = true;
            }
            else if (!hasAlpha[i] && inRange)
            {
                // 容忍小於 8 像素的微小透明縫隙（防範子圖內部有完全透明的直/橫線）
                var gapSize = 0;
                var isRealGap = true;
                for (var j = i; j < hasAlpha.Length && j < i + 8; j++)
                {
                    if (hasAlpha[j])
                    {
                        isRealGap = false;
                        break;
                    }
                    gapSize++;
                }

                if (isRealGap)
                {
                    ranges.Add((start, i));
                    inRange = false;
                }
                else
                {
                    i += gapSize;
                }
            }
        }

        if (inRange)
        {
            ranges.Add((start, hasAlpha.Length));
        }

        return ranges;
    }
}
