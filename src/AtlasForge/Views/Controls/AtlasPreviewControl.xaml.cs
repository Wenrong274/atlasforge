using System.Windows;
using System.Windows.Media;
using AtlasForge.ViewModels;

namespace AtlasForge.Views.Controls;

public partial class AtlasPreviewControl : System.Windows.Controls.UserControl
{
    private double _zoom = 1.0;

    public AtlasPreviewControl() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private void AtlasTab_Click(object sender, RoutedEventArgs e)
    {
        VM?.StopAnimation();
        AtlasTabBtn.Style = (Style)FindResource("TabActiveStyle");
        AnimTabBtn.Style = (Style)FindResource("TabInactiveStyle");

        if (VM?.CurrentAtlas is not null)
        {
            PreviewImage.Source = VM.AtlasPreview;
        }
    }

    private void AnimTab_Click(object sender, RoutedEventArgs e)
    {
        AnimTabBtn.Style = (Style)FindResource("TabActiveStyle");
        AtlasTabBtn.Style = (Style)FindResource("TabInactiveStyle");
        VM?.ToggleAnimationCommand.Execute(null);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.25);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.25);

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.1, 8.0);
        PreviewImage.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomLabel.Text = $"{_zoom:P0}";
    }
}
