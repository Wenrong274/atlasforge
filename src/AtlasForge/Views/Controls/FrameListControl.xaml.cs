using System.Windows;
using System.Windows.Input;

using AtlasForge.Services;
using AtlasForge.ViewModels;

using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace AtlasForge.Views.Controls;

public partial class FrameListControl : System.Windows.Controls.UserControl
{
    private FrameItemViewModel? _dragItem;
    private WpfPoint _dragStart;

    public FrameListControl() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private void DropZone_DragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void DropZone_Drop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && VM is not null)
        {
            await VM.LoadFramesAsync(PngFiles(files));
        }
    }

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (e.OriginalSource as FrameworkElement)?.DataContext as FrameItemViewModel;
    }

    private void List_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(position.Y - _dragStart.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            System.Windows.DragDrop.DoDragDrop(FrameListBox, _dragItem, System.Windows.DragDropEffects.Move);
        }
    }

    private async void List_Drop(object sender, WpfDragEventArgs e)
    {
        if (VM is null)
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            await VM.LoadFramesAsync(PngFiles(files));
            return;
        }

        if (_dragItem is null)
        {
            return;
        }

        var target = (e.OriginalSource as FrameworkElement)?.DataContext as FrameItemViewModel;
        if (target is null || ReferenceEquals(target, _dragItem))
        {
            return;
        }

        var oldIndex = VM.Frames.IndexOf(_dragItem);
        var newIndex = VM.Frames.IndexOf(target);
        if (oldIndex >= 0 && newIndex >= 0)
        {
            VM.Frames.Move(oldIndex, newIndex);
            ReindexFrames();
            await VM.PackAsync();
        }

        _dragItem = null;
    }

    private async void SortButton_Click(object sender, RoutedEventArgs e)
    {
        if (VM is null)
        {
            return;
        }

        var sorted = VM.Frames
            .OrderBy(frame => frame.DisplayName, new NaturalSortComparer())
            .ToList();
        VM.Frames.Clear();
        foreach (var frame in sorted)
        {
            VM.Frames.Add(frame);
        }

        ReindexFrames();
        await VM.PackAsync();
    }

    private void ReindexFrames()
    {
        if (VM is null)
        {
            return;
        }

        for (var index = 0; index < VM.Frames.Count; index++)
        {
            VM.Frames[index].OrderIndex = index;
        }
    }

    private static IEnumerable<string> PngFiles(IEnumerable<string> files) =>
        files.Where(file => file.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
}