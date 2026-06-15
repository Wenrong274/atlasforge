using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AtlasForge.Models;
using AtlasForge.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AtlasForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ImageProcessingService _imageProcessor = new();
    private readonly GridPackingService _gridPacker = new();
    private readonly BinPackingService _binPacker = new();
    private readonly ExportService _exporter = new();
    private readonly PreviewService _preview = new();
    private DispatcherTimer? _animationTimer;

    [ObservableProperty]
    private ObservableCollection<FrameItemViewModel> _frames = new();

    [ObservableProperty]
    private AtlasData? _currentAtlas;

    [ObservableProperty]
    private BitmapSource? _atlasPreview;

    [ObservableProperty]
    private string _statusMessage = "幀未載入";

    [ObservableProperty]
    private int _animationFrameIndex;

    [ObservableProperty]
    private bool _isAnimationPlaying;

    [ObservableProperty]
    private FrameItemViewModel? _selectedFrame;

    [ObservableProperty]
    private PackingMode _packingMode = PackingMode.Grid;

    [ObservableProperty]
    private bool _autoGrid = true;

    [ObservableProperty]
    private int _gridColumns = 4;

    [ObservableProperty]
    private int _gridRows = 4;

    [ObservableProperty]
    private bool _alphaTrim = true;

    [ObservableProperty]
    private int _padding = 2;

    [ObservableProperty]
    private int _maxAtlasSize = 2048;

    [ObservableProperty]
    private bool _exportPng = true;

    [ObservableProperty]
    private bool _exportJson;

    [ObservableProperty]
    private bool _exportPlist;

    [ObservableProperty]
    private string _outputPath = "";

    partial void OnPackingModeChanged(PackingMode value) => _ = PackAsync();
    partial void OnAutoGridChanged(bool value) => _ = PackAsync();
    partial void OnGridColumnsChanged(int value) => _ = PackAsync();
    partial void OnGridRowsChanged(int value) => _ = PackAsync();
    partial void OnAlphaTrimChanged(bool value) => _ = PackAsync();
    partial void OnPaddingChanged(int value) => _ = PackAsync();
    partial void OnMaxAtlasSizeChanged(int value) => _ = PackAsync();

    public async Task LoadFramesAsync(IEnumerable<string> paths)
    {
        var sorted = paths
            .OrderBy(path => Path.GetFileName(path), new NaturalSortComparer())
            .ToList();

        foreach (var (path, index) in sorted.Select((path, index) => (path, index)))
        {
            Frames.Add(FrameItemViewModel.FromFilePath(path, Frames.Count + index));
        }

        StatusMessage = $"✓ {Frames.Count} 幀已載入";
        await PackAsync();
    }

    [RelayCommand]
    public void ClearFrames()
    {
        StopAnimation();
        Frames.Clear();
        CurrentAtlas = null;
        AtlasPreview = null;
        SelectedFrame = null;
        AnimationFrameIndex = 0;
        StatusMessage = "幀未載入";
    }

    [RelayCommand]
    public void RemoveSelectedFrame()
    {
        if (SelectedFrame is null)
        {
            return;
        }

        Frames.Remove(SelectedFrame);
        SelectedFrame = null;
        ReindexFrames();
        _ = PackAsync();
    }

    public async Task PackAsync()
    {
        if (Frames.Count == 0)
        {
            return;
        }

        var paths = Frames.Select(frame => frame.FilePath).ToList();
        var frameCount = paths.Count;
        var settings = BuildSettings();

        try
        {
            var result = await Task.Run(() =>
            {
                var frameData = paths.Select(_imageProcessor.LoadFrame).ToList();
                var normalized = _imageProcessor.NormalizeFrames(frameData, AlphaTrim, Padding);
                var atlas = settings.PackingMode == PackingMode.Grid
                    ? _gridPacker.Pack(normalized, settings)
                    : _binPacker.Pack(normalized, settings);
                var preview = _preview.RenderAtlas(atlas);
                var isPowerOfTwo = IsPowerOfTwo(atlas.Atlas.Width) && IsPowerOfTwo(atlas.Atlas.Height);
                var emptyCells = atlas.Mode == PackingMode.Grid
                    ? atlas.Columns * atlas.Rows - frameCount
                    : 0;

                return (atlas, preview, isPowerOfTwo, emptyCells);
            });

            RunOnUiThread(() =>
            {
                CurrentAtlas = result.atlas;
                AtlasPreview = result.preview;
                StatusMessage = $"{result.atlas.Atlas.Width}×{result.atlas.Atlas.Height}" +
                    $" · {(result.atlas.Mode == PackingMode.Grid ? $"{result.atlas.Columns}×{result.atlas.Rows} Grid" : "BinPack")}" +
                    $" · {(result.isPowerOfTwo ? "✓PoT" : "⚠ 非PoT")}" +
                    (result.emptyCells > 0 ? $" · ⚠{result.emptyCells} 格空白" : "");
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => StatusMessage = $"⚠ {ex.Message}");
        }
    }

    [RelayCommand]
    public void Export()
    {
        if (CurrentAtlas is null || string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "⚠ 請先設定輸出路徑";
            return;
        }

        var baseName = Frames.Count > 0
            ? Path.GetFileNameWithoutExtension(Frames[0].FilePath)
                .TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_')
            : "atlas";

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "atlas";
        }

        _exporter.Export(CurrentAtlas, BuildSettings(), baseName);
        StatusMessage = $"✓ 匯出完成 → {Path.Combine(OutputPath, $"{baseName}.png")}";
    }

    [RelayCommand]
    public void ToggleAnimation()
    {
        if (IsAnimationPlaying)
        {
            StopAnimation();
        }
        else
        {
            StartAnimation();
        }
    }

    public void StepFrame(int delta)
    {
        if (CurrentAtlas is null || CurrentAtlas.Frames.Count == 0)
        {
            return;
        }

        AnimationFrameIndex = (AnimationFrameIndex + delta + CurrentAtlas.Frames.Count) % CurrentAtlas.Frames.Count;
        AtlasPreview = _preview.RenderFrame(CurrentAtlas, AnimationFrameIndex);
    }

    private void StartAnimation()
    {
        if (CurrentAtlas is null || CurrentAtlas.Frames.Count == 0)
        {
            return;
        }

        _animationTimer ??= new DispatcherTimer();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Interval = TimeSpan.FromSeconds(1.0 / 12);
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
        IsAnimationPlaying = true;
    }

    public void StopAnimation()
    {
        if (_animationTimer is not null)
        {
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer.Stop();
        }

        IsAnimationPlaying = false;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        StepFrame(1);
    }

    private ExportSettings BuildSettings() => new()
    {
        PackingMode = PackingMode,
        AutoGrid = AutoGrid,
        GridColumns = GridColumns,
        GridRows = GridRows,
        AlphaTrim = AlphaTrim,
        Padding = Padding,
        MaxAtlasSize = MaxAtlasSize,
        ExportPng = ExportPng,
        ExportJson = ExportJson,
        ExportPlist = ExportPlist,
        OutputPath = OutputPath
    };

    private void ReindexFrames()
    {
        for (var index = 0; index < Frames.Count; index++)
        {
            Frames[index].OrderIndex = index;
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
}
