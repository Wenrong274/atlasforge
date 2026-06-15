using System.Windows;
using AtlasForge.ViewModels;

namespace AtlasForge.Views.Controls;

public partial class SettingsPanel : System.Windows.Controls.UserControl
{
    public SettingsPanel() => InitializeComponent();

    private MainViewModel? VM => DataContext as MainViewModel;

    private void BrowsePath_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "選擇輸出資料夾",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && VM is not null)
        {
            VM.OutputPath = dialog.SelectedPath;
        }
    }
}
