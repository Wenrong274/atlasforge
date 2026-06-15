namespace AtlasForge.Models;

public enum PackingMode
{
    Grid,
    BinPack
}

public class ExportSettings
{
    public bool ExportPng { get; set; } = true;
    public bool ExportJson { get; set; } = false;
    public bool ExportPlist { get; set; } = false;
    public string OutputPath { get; set; } = "";
    public PackingMode PackingMode { get; set; } = PackingMode.Grid;
    public bool AutoGrid { get; set; } = true;
    public int GridColumns { get; set; } = 4;
    public int GridRows { get; set; } = 4;
    public bool AlphaTrim { get; set; } = true;
    public int Padding { get; set; } = 2;
    public int MaxAtlasSize { get; set; } = 2048;
}
