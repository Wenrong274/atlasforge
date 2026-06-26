using System.IO;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;

using SkiaSharp;

namespace AtlasForge.ViewModels;

public partial class FrameItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private BitmapSource? _thumbnail;

    [ObservableProperty]
    private int _orderIndex;

    public static FrameItemViewModel FromFilePath(string filePath, int index)
    {
        var viewModel = new FrameItemViewModel
        {
            FilePath = filePath,
            DisplayName = Path.GetFileName(filePath),
            OrderIndex = index
        };
        viewModel.LoadThumbnail();

        return viewModel;
    }

    private void LoadThumbnail()
    {
        using var bitmap = SKBitmap.Decode(FilePath);
        if (bitmap is null)
        {
            return;
        }

        using var scaled = bitmap.Resize(new SKImageInfo(32, 32), SKFilterQuality.Medium);
        if (scaled is null)
        {
            return;
        }

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = stream;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        Thumbnail = bitmapImage;
    }
}
