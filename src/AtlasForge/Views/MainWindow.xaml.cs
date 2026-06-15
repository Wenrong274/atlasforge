using System.IO;
using System.Windows.Input;
using AtlasForge.ViewModels;

namespace AtlasForge.Views;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private async void OpenFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "選擇含 PNG 幀的資料夾",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && VM is not null)
        {
            var pngs = Directory.GetFiles(dialog.SelectedPath, "*.png", SearchOption.TopDirectoryOnly);
            await VM.LoadFramesAsync(pngs);
        }
    }

    private async void OpenFiles_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "選擇 PNG 幀",
            Filter = "PNG 圖片|*.png",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true && VM is not null)
        {
            await VM.LoadFramesAsync(dialog.FileNames);
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenFolder_Click(this, new System.Windows.RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space)
        {
            VM?.ToggleAnimationCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            VM?.StepFrame(-1);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            VM?.StepFrame(1);
            e.Handled = true;
        }
    }
}
