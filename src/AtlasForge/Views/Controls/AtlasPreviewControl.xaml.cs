using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

using AtlasForge.ViewModels;

namespace AtlasForge.Views.Controls;

public partial class AtlasPreviewControl : System.Windows.Controls.UserControl
{
    private double _zoom = 1.0;
    private bool _isDragging;
    private System.Windows.Point _dragStart;

    public AtlasPreviewControl() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private void AtlasTab_Click(object _, RoutedEventArgs e)
    {
        VM?.ShowAtlasView();
        AtlasTabBtn.Style = (Style)FindResource("TabActiveStyle");
        AnimTabBtn.Style = (Style)FindResource("TabInactiveStyle");
    }

    private void AnimTab_Click(object _, RoutedEventArgs e)
    {
        AnimTabBtn.Style = (Style)FindResource("TabActiveStyle");
        AtlasTabBtn.Style = (Style)FindResource("TabInactiveStyle");
        VM?.ToggleAnimationCommand.Execute(null);
    }

    private void ZoomIn_Click(object _, RoutedEventArgs e) => SetZoom(_zoom * 1.25);

    private void ZoomOut_Click(object _, RoutedEventArgs e) => SetZoom(_zoom / 1.25);

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.1, 8.0);
        PreviewImage.LayoutTransform = new ScaleTransform(_zoom, _zoom);
        ZoomLabel.Text = $"{_zoom:P0}";
    }

    private void SetZoomAt(double newZoom, System.Windows.Point mousePos)
    {
        // 縮放前游標對應的內容座標
        double contentX = (PreviewScroll.HorizontalOffset + mousePos.X) / _zoom;
        double contentY = (PreviewScroll.VerticalOffset + mousePos.Y) / _zoom;

        SetZoom(newZoom);

        // layout 刷新後再移 scroll offset，維持游標錨點
        Dispatcher.Invoke(DispatcherPriority.Render, () =>
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

    private void PreviewScroll_MouseEnter(object _, System.Windows.Input.MouseEventArgs e) => AnimateHints(0.15);

    private void PreviewScroll_MouseLeave(object _, System.Windows.Input.MouseEventArgs e)
    {
        _isDragging = false;
        PreviewScroll.Cursor = System.Windows.Input.Cursors.Arrow;
        AnimateHints(1.0);
    }

    private void PreviewScroll_MouseLeftButtonDown(object _, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _dragStart = e.GetPosition(PreviewScroll);
        PreviewScroll.CaptureMouse();
        PreviewScroll.Cursor = System.Windows.Input.Cursors.SizeAll;
    }

    private void PreviewScroll_MouseMove(object _, System.Windows.Input.MouseEventArgs e)
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
        PreviewScroll.Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private void AnimateHints(double toOpacity)
    {
        var anim = new DoubleAnimation(toOpacity, TimeSpan.FromSeconds(0.2));
        HintsOverlay.BeginAnimation(OpacityProperty, anim);
    }
}