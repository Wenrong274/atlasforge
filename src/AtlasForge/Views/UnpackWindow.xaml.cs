using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AtlasForge.Models;
using AtlasForge.Services;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace AtlasForge.Views;

public partial class UnpackWindow : Window
{
    private readonly UnpackService _unpackService = new();
    private BitmapImage? _loadedImage;
    private List<SliceItem> _slices = new();
    private double _zoom = 1.0;

    public UnpackWindow()
    {
        InitializeComponent();
        PreviewScroll.PreviewMouseWheel += PreviewScroll_MouseWheel;
    }

    private void BrowseImage_Click(object _, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PNG Images|*.png" };
        if (dialog.ShowDialog(this) == true)
        {
            LoadImage(dialog.FileName);
        }
    }

    private void BrowseDesc_Click(object _, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Description Files|*.json;*.plist" };
        if (dialog.ShowDialog(this) == true)
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
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();

            _loadedImage = img;
            PreviewImage.Source = img;
            HighlightCanvas.Children.Clear();
            SliceListBox.ItemsSource = null;

            // 自動帶入輸出資料夾
            var dir = Path.GetDirectoryName(path) ?? "";
            var name = Path.GetFileNameWithoutExtension(path);
            OutputDirTxt.Text = Path.Combine(dir, $"{name}_sliced");
            StatusLbl.Text = "合圖已載入，請設定參數後點擊預覽分析。";
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

                _slices = _unpackService.GenerateGridSlices(_loadedImage.PixelWidth, _loadedImage.PixelHeight, cols, rows, _loadedImage);
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
            Width = selected.Width,
            Height = selected.Height,
            BorderThickness = new Thickness(2),
            BorderBrush = (Brush)FindResource("PurpleAccent") ?? new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            Background = (Brush)FindResource("PurpleSelection") ?? new SolidColorBrush(Color.FromArgb(40, 139, 92, 246)),
            IsHitTestVisible = false
        };

        Canvas.SetLeft(border, selected.X);
        Canvas.SetTop(border, selected.Y);
        HighlightCanvas.Children.Add(border);
    }

    private void PreviewScroll_MouseWheel(object _, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;
        var factor = e.Delta > 0 ? 1.25 : 1.0 / 1.25;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
        ImageContainer.LayoutTransform = new ScaleTransform(_zoom, _zoom);
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
}
