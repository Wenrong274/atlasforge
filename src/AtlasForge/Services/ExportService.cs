using System.IO;
using System.Text.Json;
using System.Xml.Linq;

using AtlasForge.Models;

using SkiaSharp;

namespace AtlasForge.Services;

public class ExportService
{
    public void Export(AtlasData atlasData, ExportSettings settings, string baseName)
    {
        Directory.CreateDirectory(settings.OutputPath);

        if (settings.ExportPng)
        {
            ExportPng(atlasData.Atlas, Path.Combine(settings.OutputPath, $"{baseName}.png"));
        }

        if (settings.ExportJson)
        {
            ExportJson(atlasData, $"{baseName}.png", Path.Combine(settings.OutputPath, $"{baseName}.json"));
        }

        if (settings.ExportPlist)
        {
            ExportPlist(atlasData, $"{baseName}.png", Path.Combine(settings.OutputPath, $"{baseName}.plist"));
        }
    }

    public void ExportPng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        encoded.SaveTo(stream);
    }

    public void ExportJson(AtlasData atlasData, string imageFilename, string path)
    {
        var frames = atlasData.Frames.ToDictionary(
            frame => frame.Name,
            frame => (object)new
            {
                frame = new { x = frame.X, y = frame.Y, w = frame.Width, h = frame.Height },
                rotated = false,
                trimmed = frame.OffsetX != 0 || frame.OffsetY != 0,
                spriteSourceSize = new
                {
                    x = frame.OffsetX,
                    y = frame.OffsetY,
                    w = frame.Width,
                    h = frame.Height
                },
                sourceSize = new { w = frame.SourceWidth, h = frame.SourceHeight }
            });

        var document = new
        {
            frames,
            meta = new
            {
                app = "AtlasForge",
                version = "1.0.0",
                image = imageFilename,
                format = "RGBA8888",
                size = new { w = atlasData.Atlas.Width, h = atlasData.Atlas.Height },
                scale = "1"
            }
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void ExportPlist(AtlasData atlasData, string imageFilename, string path)
    {
        var framesDict = new XElement("dict");
        foreach (var frame in atlasData.Frames)
        {
            framesDict.Add(
                new XElement("key", $"{frame.Name}.png"),
                new XElement("dict",
                    new XElement("key", "frame"),
                    new XElement("string", $"{{{{{frame.X},{frame.Y}}},{{{frame.Width},{frame.Height}}}}}"),
                    new XElement("key", "offset"),
                    new XElement("string", $"{{{frame.OffsetX},{frame.OffsetY}}}"),
                    new XElement("key", "rotated"),
                    new XElement("false"),
                    new XElement("key", "sourceColorRect"),
                    new XElement("string", $"{{{{{frame.OffsetX},{frame.OffsetY}}},{{{frame.Width},{frame.Height}}}}}"),
                    new XElement("key", "sourceSize"),
                    new XElement("string", $"{{{frame.SourceWidth},{frame.SourceHeight}}}")));
        }

        var metadataDict = new XElement("dict",
            new XElement("key", "format"),
            new XElement("integer", "2"),
            new XElement("key", "realTextureFileName"),
            new XElement("string", imageFilename),
            new XElement("key", "size"),
            new XElement("string", $"{{{atlasData.Atlas.Width},{atlasData.Atlas.Height}}}"),
            new XElement("key", "textureFileName"),
            new XElement("string", imageFilename));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType(
                "plist",
                "-//Apple//DTD PLIST 1.0//EN",
                "http://www.apple.com/DTDs/PropertyList-1.0.dtd",
                null),
            new XElement("plist",
                new XAttribute("version", "1.0"),
                new XElement("dict",
                    new XElement("key", "frames"),
                    framesDict,
                    new XElement("key", "metadata"),
                    metadataDict)));

        document.Save(path);
    }
}