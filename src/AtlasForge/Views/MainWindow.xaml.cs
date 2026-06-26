using System.IO;
using System.Windows;
using System.Windows.Input;

using AtlasForge.ViewModels;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace AtlasForge.Views;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private async void OpenFolder_Click(object _, System.Windows.RoutedEventArgs e)
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


    private bool _isUnpackerMode;

    private void PackerTab_Click(object _, System.Windows.RoutedEventArgs e)
    {
        SwitchToMode(false);
    }

    private void UnpackerTab_Click(object _, System.Windows.RoutedEventArgs e)
    {
        SwitchToMode(true);
    }

    private void SwitchToMode(bool isUnpacker)
    {
        _isUnpackerMode = isUnpacker;

        if (isUnpacker)
        {
            // 切換至拆圖模式
            PackerMainGrid.Visibility = System.Windows.Visibility.Collapsed;
            UnpackerMainGrid.Visibility = System.Windows.Visibility.Visible;
            PackerToolbar.Visibility = System.Windows.Visibility.Collapsed;

            PackerTabBtn.Style = (Style)FindResource("TabInactiveStyle");
            UnpackerTabBtn.Style = (Style)FindResource("TabActiveStyle");

            Title = "AtlasForge (拆圖模式)";
            TitleLbl.Text = "🔧 AtlasForge";
            TitleLbl.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));

            // 動態變更主題顏色為科技紫
            Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));
            Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));
            Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2E5C"));
        }
        else
        {
            // 切換至合圖打包模式
            PackerMainGrid.Visibility = System.Windows.Visibility.Visible;
            UnpackerMainGrid.Visibility = System.Windows.Visibility.Collapsed;
            PackerToolbar.Visibility = System.Windows.Visibility.Visible;

            PackerTabBtn.Style = (Style)FindResource("TabActiveStyle");
            UnpackerTabBtn.Style = (Style)FindResource("TabInactiveStyle");

            Title = "AtlasForge";
            TitleLbl.Text = "⚡ AtlasForge";
            TitleLbl.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));

            // 動態還原為科技藍
            Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
            Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0369A1"));
            Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E3A5F"));
        }
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_isUnpackerMode)
        {
            return; // 拆圖模式下停用打包快速鍵
        }

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
