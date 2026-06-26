using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AtlasForge.Models;
using AtlasForge.Services;
using SkiaSharp;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Point = System.Windows.Point;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using UserControl = System.Windows.Controls.UserControl;

namespace AtlasForge.Views.Controls;

public partial class UnpackerControl : UserControl
{
    private readonly UnpackService _unpackService = new();
    private BitmapSource? _loadedImage;
    private List<SliceItem> _slices = new();
    private double _zoom = 1.0;
    private bool _isDragging;
    private Point _dragStart;

    public UnpackerControl()
    {
        InitializeComponent();
        PreviewScroll.PreviewMouseWheel += PreviewScroll_MouseWheel;
    }

    private static BitmapSource BitmapFromSKBitmap(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        // Ensure we work with Bgra8888 for direct compatibility with WPF's PixelFormats.Bgra32
        using var bgraBitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bgraBitmap))
        {
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        var writeableBitmap = new WriteableBitmap(
            width,
            height,
            96.0, // Force 96 DPI
            96.0, // Force 96 DPI
            PixelFormats.Bgra32,
            null
        );

        writeableBitmap.WritePixels(
            new Int32Rect(0, 0, width, height),
            bgraBitmap.GetPixels(),
            bgraBitmap.RowBytes * height,
            bgraBitmap.RowBytes
        );

        writeableBitmap.Freeze();
        return writeableBitmap;
    }

    private void BrowseImage_Click(object _, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PNG Images|*.png" };
        var owner = Window.GetWindow(this);
        if (owner != null && dialog.ShowDialog(owner) == true)
        {
            LoadImage(dialog.FileName);
        }
        else if (owner == null && dialog.ShowDialog() == true)
        {
            LoadImage(dialog.FileName);
        }
    }

    private void BrowseDesc_Click(object _, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Description Files|*.json;*.plist" };
        var owner = Window.GetWindow(this);
        if (owner != null && dialog.ShowDialog(owner) == true)
        {
            DescPathTxt.Text = dialog.FileName;
        }
        else if (owner == null && dialog.ShowDialog() == true)
        {
            DescPathTxt.Text = dialog.FileName;
        }
    }

    private void BrowseOutput_Click(object _, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputDirTxt.Text = dialog.SelectedPath;
        }
    }

    private void Txt_DragOver(object _, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Image_Drop(object _, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && files[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            LoadImage(files[0]);
        }
    }

    private void Desc_Drop(object _, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            var ext = Path.GetExtension(files[0]).ToLower();
            if (ext == ".json" || ext == ".plist")
            {
                DescPathTxt.Text = files[0];
            }
        }
    }

    private void LoadImage(string path)
    {
        try
        {
            ImagePathTxt.Text = path;

            // 使用 SkiaSharp 解碼圖片以支援中文/空格路徑，並強制將 DPI 重設為 96
            using var fileStream = File.OpenRead(path);
            using var skiaBitmap = SKBitmap.Decode(fileStream);
            if (skiaBitmap is null)
            {
                throw new InvalidOperationException("無法解碼合圖點陣圖。");
            }

            var img = BitmapFromSKBitmap(skiaBitmap);

            _loadedImage = img;
            PreviewImage.Source = img;
            HighlightCanvas.Children.Clear();
            SliceListBox.ItemsSource = null;

            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            OutputDirTxt.Text = Path.Combine(dir, $"{name}_sliced");

            // 自動搜尋同目錄同主檔名的描述檔
            var plistPath = Path.Combine(dir, $"{name}.plist");
            var jsonPath = Path.Combine(dir, $"{name}.json");

            if (File.Exists(plistPath))
            {
                DescPathTxt.Text = plistPath;
                ModeDescRadio.IsChecked = true;
                Mode_Checked(this, new RoutedEventArgs());
                StatusLbl.Text = $"合圖已載入，已自動偵測並載入描述檔: {name}.plist";
            }
            else if (File.Exists(jsonPath))
            {
                DescPathTxt.Text = jsonPath;
                ModeDescRadio.IsChecked = true;
                Mode_Checked(this, new RoutedEventArgs());
                StatusLbl.Text = $"合圖已載入，已自動偵測並載入描述檔: {name}.json";
            }
            else
            {
                StatusLbl.Text = $"合圖已載入: {img.PixelWidth}x{img.PixelHeight} (DPI: {img.DpiX:F1}x{img.DpiY:F1})，請設定參數後預覽。";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"載入圖片失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Mode_Checked(object _, RoutedEventArgs e)
    {
        if (DescPanel is null || GridPanel is null)
        {
            return;
        }

        if (ModeDescRadio.IsChecked == true)
        {
            DescPanel.Visibility = Visibility.Visible;
            GridPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            DescPanel.Visibility = Visibility.Collapsed;
            GridPanel.Visibility = Visibility.Visible;
        }
    }

    private void GridSettings_TextChanged(object _, TextChangedEventArgs e)
    {
        if (SliceListBox is not null)
        {
            SliceListBox.ItemsSource = null;
        }
        if (HighlightCanvas is not null)
        {
            HighlightCanvas.Children.Clear();
        }
    }

    private void Preview_Click(object _, RoutedEventArgs e)
    {
        if (_loadedImage is null)
        {
            MessageBox.Show("請先選擇合圖檔案！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (ModeDescRadio.IsChecked == true)
            {
                var desc = DescPathTxt.Text;
                if (string.IsNullOrEmpty(desc) || !File.Exists(desc))
                {
                    MessageBox.Show("請選擇正確的描述檔！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var ext = Path.GetExtension(desc).ToLower();
                if (ext == ".json")
                {
                    _slices = _unpackService.ParseJson(desc, _loadedImage);
                }
                else if (ext == ".plist")
                {
                    _slices = _unpackService.ParsePlist(desc, _loadedImage);
                }
                else
                {
                    MessageBox.Show("不支援的描述檔格式！僅支援 .json 或 .plist", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                if (!int.TryParse(GridColsTxt.Text, out var cols) || cols <= 0 ||
                    !int.TryParse(GridRowsTxt.Text, out var rows) || rows <= 0)
                {
                    MessageBox.Show("請輸入正確的網格行數與列數（正整數）！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var padding = 0;
                if (GridPaddingTxt is not null && (!int.TryParse(GridPaddingTxt.Text, out padding) || padding < 0))
                {
                    padding = 0;
                }

                _slices = _unpackService.GenerateGridSlices(_loadedImage.PixelWidth, _loadedImage.PixelHeight, cols, rows, padding, _loadedImage);
            }

            SliceListBox.ItemsSource = _slices;
            StatusLbl.Text = $"🔍 預估可拆分為 {_slices.Count} 個圖檔";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"解析失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SliceListBox_SelectionChanged(object _, SelectionChangedEventArgs e)
    {
        HighlightCanvas.Children.Clear();
        if (SliceListBox.SelectedItem is not SliceItem selected)
        {
            return;
        }

        var border = new Border
        {
            Width = selected.Rotated ? selected.Height : selected.Width,
            Height = selected.Rotated ? selected.Width : selected.Height,
            BorderThickness = new Thickness(2),
            BorderBrush = (Brush)FindResource("PurpleAccent") ?? new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            Background = (Brush)FindResource("PurpleSelection") ?? new SolidColorBrush(Color.FromArgb(40, 139, 92, 246)),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(border, selected.X);
        Canvas.SetTop(border, selected.Y);
        HighlightCanvas.Children.Add(border);
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.1, 8.0);
        ImageContainer.LayoutTransform = new ScaleTransform(_zoom, _zoom);
    }

    private void SetZoomAt(double newZoom, Point mousePos)
    {
        double contentX = (PreviewScroll.HorizontalOffset + mousePos.X) / _zoom;
        double contentY = (PreviewScroll.VerticalOffset + mousePos.Y) / _zoom;

        SetZoom(newZoom);

        Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            PreviewScroll.ScrollToHorizontalOffset(contentX * _zoom - mousePos.X);
            PreviewScroll.ScrollToVerticalOffset(contentY * _zoom - mousePos.Y);
        });
    }

    private void PreviewScroll_MouseWheel(object _, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;
        var factor = e.Delta > 0 ? 1.25 : 1.0 / 1.25;
        SetZoomAt(Math.Clamp(_zoom * factor, 0.1, 8.0), e.GetPosition(PreviewScroll));
    }

    private void PreviewScroll_MouseEnter(object _, MouseEventArgs e) => AnimateHints(0.15);

    private void PreviewScroll_MouseLeave(object _, MouseEventArgs e)
    {
        _isDragging = false;
        PreviewScroll.Cursor = Cursors.Arrow;
        AnimateHints(1.0);
    }

    private void PreviewScroll_MouseLeftButtonDown(object _, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(PreviewScroll);
        PreviewScroll.CaptureMouse();
        PreviewScroll.Cursor = Cursors.SizeAll;
    }

    private void PreviewScroll_MouseMove(object _, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var pos = e.GetPosition(PreviewScroll);
        var delta = _dragStart - pos;
        _dragStart = pos;
        PreviewScroll.ScrollToHorizontalOffset(PreviewScroll.HorizontalOffset + delta.X);
        PreviewScroll.ScrollToVerticalOffset(PreviewScroll.VerticalOffset + delta.Y);
    }

    private void PreviewScroll_MouseLeftButtonUp(object _, MouseButtonEventArgs e)
    {
        _isDragging = false;
        PreviewScroll.ReleaseMouseCapture();
        PreviewScroll.Cursor = Cursors.Arrow;
    }

    private void AnimateHints(double toOpacity)
    {
        var anim = new System.Windows.Media.Animation.DoubleAnimation(toOpacity, TimeSpan.FromSeconds(0.2));
        HintsOverlay.BeginAnimation(OpacityProperty, anim);
    }

    private async void Unpack_Click(object _, RoutedEventArgs e)
    {
        if (_slices.Count == 0 || _loadedImage is null)
        {
            MessageBox.Show("請先執行「預覽分析」！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var outDir = OutputDirTxt.Text;
        if (string.IsNullOrEmpty(outDir))
        {
            MessageBox.Show("請選擇輸出資料夾！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            UnpackProgress.Visibility = Visibility.Visible;
            UnpackProgress.IsIndeterminate = true;
            StatusLbl.Text = "正在匯出圖片中...";

            var imgPath = ImagePathTxt.Text;
            var slicesCopy = _slices.ToList();

            await Task.Run(() => _unpackService.SaveSlices(imgPath, slicesCopy, outDir));

            UnpackProgress.Visibility = Visibility.Collapsed;
            StatusLbl.Text = $"✓ 拆分完成，共導出 {slicesCopy.Count} 個圖檔！";
            MessageBox.Show($"成功導出 {slicesCopy.Count} 個序列圖至:\n{outDir}", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UnpackProgress.Visibility = Visibility.Collapsed;
            StatusLbl.Text = "⚠ 拆分失敗";
            MessageBox.Show($"拆分圖片發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutoDetectGrid_Click(object sender, RoutedEventArgs e)
    {
        var path = ImagePathTxt.Text;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show("請先載入正確的合圖檔案！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var fileStream = File.OpenRead(path);
            using var skiaBitmap = SKBitmap.Decode(fileStream);
            if (skiaBitmap is null)
            {
                throw new InvalidOperationException("無法解碼合圖點陣圖。");
            }

            var (cols, rows, padding) = _unpackService.DetectGrid(skiaBitmap);
            GridColsTxt.Text = cols.ToString();
            GridRowsTxt.Text = rows.ToString();
            GridPaddingTxt.Text = padding.ToString();

            StatusLbl.Text = $"🪄 網格自動偵測完成：{cols} 行 {rows} 列，建議間距：{padding}px";

            // 自動觸發預覽分析
            Preview_Click(this, new RoutedEventArgs());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"自動偵測網格失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
