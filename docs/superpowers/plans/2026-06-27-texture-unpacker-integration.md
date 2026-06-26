# Texture Unpacker Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 移除獨立的 `UnpackWindow` 彈出視窗，改為將拆圖工具整合為 `UnpackerControl` 使用者控制項，並於 `MainWindow` 頂部新增分頁切換。當切換至拆圖模式時，視窗的 Accent 主題色會從科技藍動態切換為科技紫。

**Architecture:**
1. **UserControl Conversion**: 建立 `UnpackerControl.xaml` 與 `UnpackerControl.xaml.cs`，承載原 `UnpackWindow` 的三欄式切分設定、清單與預覽邏輯。
2. **Dynamic Resources**: 修改 `AppStyles.xaml` 讓 `Accent` 與 `BorderColor` 使用 `DynamicResource` 綁定，支援執行期隨模式動態更換顏色。
3. **MainWindow Integration**: 
   - 頂部 toolbar 最左側加入切換分頁按鈕（「⚡ 合圖打包」與「🔧 序列拆圖」）。
   - 依模式隱藏/顯示 Packer 的 toolbar 按鈕。
   - 下方主要區域承載 Packer Layout 和 `UnpackerControl`，利用 visibility 進行切換。
   - Code-Behind 處理頁籤切換與動態資源變更（科技藍與科技紫色彩轉換）。
4. **Cleanup**: 刪除舊的 `UnpackWindow.xaml` 與 `UnpackWindow.xaml.cs`。

**Tech Stack:** WPF / C# 12 / .NET 8 / SkiaSharp

## Global Constraints
- C# nullable enable，implicit usings
- 事件處理常式中以 `_` 捨棄未使用的 `sender`
- UI 文字繁體中文
- 變數命名規範：私有欄位底線 camelCase，靜態唯讀/常數 PascalCase
- 每次 Commit 前需跑完 `dotnet format`

---

### Task 1: 轉換 `AppStyles.xaml` 為動態資源 (DynamicResource)

**Files:**
- Modify: `src/AtlasForge/Views/Styles/AppStyles.xaml:49-50, 102-103, 119-120, 151-152`

- [ ] **Step 1: 將 AppStyles 中的 StaticResource 替換為 DynamicResource**
  修改 `src/AtlasForge/Views/Styles/AppStyles.xaml`，將引用 `Accent` 與 `AccentHover` 的 `StaticResource` 改為 `DynamicResource`：
  
  針對 `TabActiveStyle`：
  ```xml
  <!-- 原線 49 行 -->
  <Setter Property="Background" Value="{DynamicResource Accent}" />
  ```
  
  針對 `ExportButtonStyle`：
  ```xml
  <!-- 原線 102 行 -->
  <Setter Property="Background" Value="{DynamicResource Accent}" />
  <!-- 原線 119 行 -->
  <Setter Property="Background" Value="{DynamicResource AccentHover}" />
  ```
  
  針對 `ComboBoxItem` 的 Selected Trigger：
  ```xml
  <!-- 原線 151 行 -->
  <Setter Property="Background" Value="{DynamicResource Accent}" />
  ```

- [ ] **Step 2: 執行驗證建置**
  執行: `dotnet build AtlasForge.sln`
  確認沒有語法錯誤，且程式正常建置。

- [ ] **Step 3: Commit 變更**
  ```bash
  git add src/AtlasForge/Views/Styles/AppStyles.xaml
  git commit -m "style: convert Accent resources to DynamicResource in AppStyles"
  ```

---

### Task 2: 建立 UnpackerControl 使用者控制項

**Files:**
- Create: `src/AtlasForge/Views/Controls/UnpackerControl.xaml`
- Create: `src/AtlasForge/Views/Controls/UnpackerControl.xaml.cs`

- [ ] **Step 1: 建立 UnpackerControl XAML 佈局**
  建立 `src/AtlasForge/Views/Controls/UnpackerControl.xaml`，內容大致與原先的 `UnpackWindow.xaml` 相同，但根節點為 `UserControl`：
  ```xml
  <UserControl x:Class="AtlasForge.Views.Controls.UnpackerControl"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               Background="{StaticResource BgDark}">
      <UserControl.Resources>
          <!-- 科技紫主題配色，這些可在控制項內部維持 Static/Dynamic -->
          <SolidColorBrush x:Key="PurpleAccent" Color="#8B5CF6" />
          <SolidColorBrush x:Key="PurpleAccentHover" Color="#7C3AED" />
          <SolidColorBrush x:Key="PurpleSelection" Color="#288B5CF6" />

          <!-- 紫色按鈕樣式 -->
          <Style x:Key="PurpleButtonStyle" TargetType="Button" BasedOn="{StaticResource ToolbarButtonStyle}">
              <Setter Property="Background" Value="{StaticResource PurpleAccent}" />
              <Setter Property="Foreground" Value="{StaticResource TextPrimary}" />
              <Setter Property="BorderThickness" Value="0" />
              <Setter Property="Template">
                  <Setter.Value>
                      <ControlTemplate TargetType="Button">
                          <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="8,4">
                              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                          </Border>
                      </ControlTemplate>
                  </Setter.Value>
              </Setter>
              <Style.Triggers>
                  <Trigger Property="IsMouseOver" Value="True">
                      <Setter Property="Background" Value="{StaticResource PurpleAccentHover}" />
                  </Trigger>
              </Style.Triggers>
          </Style>
      </UserControl.Resources>

      <Grid Margin="10">
          <Grid.ColumnDefinitions>
              <!-- 1. 控制欄 -->
              <ColumnDefinition Width="240" />
              <ColumnDefinition Width="5" />
              <!-- 2. 切片清單欄 -->
              <ColumnDefinition Width="190" />
              <ColumnDefinition Width="5" />
              <!-- 3. 大圖預覽欄 -->
              <ColumnDefinition Width="*" />
          </Grid.ColumnDefinitions>

          <!-- 1. 控制欄 -->
          <Grid Grid.Column="0" Margin="0,0,5,0">
              <Grid.RowDefinitions>
                  <RowDefinition Height="*" />
                  <RowDefinition Height="Auto" />
              </Grid.RowDefinitions>

              <StackPanel Grid.Row="0">
                  <TextBlock Text="🔧 拆圖參數設定" Style="{StaticResource SectionLabelStyle}" Foreground="{StaticResource PurpleAccent}" />
                  
                  <TextBlock Text="合圖路徑 (.png)" Foreground="{StaticResource TextSecondary}" Margin="0,8,0,2" FontSize="10" />
                  <Grid>
                      <Grid.ColumnDefinitions>
                          <ColumnDefinition Width="*" />
                          <ColumnDefinition Width="Auto" />
                      </Grid.ColumnDefinitions>
                      <TextBox x:Name="ImagePathTxt" Height="22" Background="{StaticResource BgLight}" Foreground="{StaticResource TextPrimary}" BorderThickness="0" Padding="3,1" AllowDrop="True" PreviewDragOver="Txt_DragOver" Drop="Image_Drop" />
                      <Button Grid.Column="1" Content="📄" Style="{StaticResource ToolbarButtonStyle}" Margin="4,0,0,0" Click="BrowseImage_Click" />
                  </Grid>

                  <TextBlock Text="切分模式" Foreground="{StaticResource TextSecondary}" Margin="0,10,0,2" FontSize="10" />
                  <StackPanel Orientation="Horizontal" Margin="0,2,0,8">
                      <RadioButton x:Name="ModeDescRadio" Content="描述檔 (JSON/Plist)" Foreground="{StaticResource TextPrimary}" IsChecked="True" Checked="Mode_Checked" Margin="0,0,10,0" />
                      <RadioButton x:Name="ModeGridRadio" Content="網格 (Grid)" Foreground="{StaticResource TextPrimary}" Checked="Mode_Checked" />
                  </StackPanel>

                  <!-- 描述檔輸入區 -->
                  <StackPanel x:Name="DescPanel">
                      <TextBlock Text="描述檔路徑" Foreground="{StaticResource TextSecondary}" Margin="0,5,0,2" FontSize="10" />
                      <Grid>
                          <Grid.ColumnDefinitions>
                              <ColumnDefinition Width="*" />
                              <ColumnDefinition Width="Auto" />
                          </Grid.ColumnDefinitions>
                          <TextBox x:Name="DescPathTxt" Height="22" Background="{StaticResource BgLight}" Foreground="{StaticResource TextPrimary}" BorderThickness="0" Padding="3,1" AllowDrop="True" PreviewDragOver="Txt_DragOver" Drop="Desc_Drop" />
                          <Button Grid.Column="1" Content="📄" Style="{StaticResource ToolbarButtonStyle}" Margin="4,0,0,0" Click="BrowseDesc_Click" />
                      </Grid>
                  </StackPanel>

                  <!-- 網格輸入區 (預設隱藏) -->
                  <StackPanel x:Name="GridPanel" Visibility="Collapsed">
                      <TextBlock Text="網格切分設定" Foreground="{StaticResource TextSecondary}" Margin="0,5,0,2" FontSize="10" />
                      <Grid Margin="0,2">
                          <Grid.ColumnDefinitions>
                              <ColumnDefinition Width="*" />
                              <ColumnDefinition Width="10" />
                              <ColumnDefinition Width="*" />
                          </Grid.ColumnDefinitions>
                          <StackPanel Grid.Column="0">
                              <TextBlock Text="行數 (Columns)" Foreground="{StaticResource TextMuted}" FontSize="9" />
                              <TextBox x:Name="GridColsTxt" Text="4" Height="22" Background="{StaticResource BgLight}" Foreground="{StaticResource TextPrimary}" BorderThickness="0" Padding="3,1" TextChanged="GridSettings_TextChanged" />
                          </StackPanel>
                          <StackPanel Grid.Column="2">
                              <TextBlock Text="列數 (Rows)" Foreground="{StaticResource TextMuted}" FontSize="9" />
                              <TextBox x:Name="GridRowsTxt" Text="4" Height="22" Background="{StaticResource BgLight}" Foreground="{StaticResource TextPrimary}" BorderThickness="0" Padding="3,1" TextChanged="GridSettings_TextChanged" />
                          </StackPanel>
                      </Grid>
                  </StackPanel>

                  <TextBlock Text="輸出資料夾" Foreground="{StaticResource TextSecondary}" Margin="0,10,0,2" FontSize="10" />
                  <Grid>
                      <Grid.ColumnDefinitions>
                          <ColumnDefinition Width="*" />
                          <ColumnDefinition Width="Auto" />
                      </Grid.ColumnDefinitions>
                      <TextBox x:Name="OutputDirTxt" Height="22" Background="{StaticResource BgLight}" Foreground="{StaticResource TextPrimary}" BorderThickness="0" Padding="3,1" />
                      <Button Grid.Column="1" Content="📁" Style="{StaticResource ToolbarButtonStyle}" Margin="4,0,0,0" Click="BrowseOutput_Click" />
                  </Grid>
              </StackPanel>

              <StackPanel Grid.Row="1" Margin="0,10,0,0">
                  <TextBlock x:Name="StatusLbl" Text="等待預覽分析..." Foreground="{StaticResource TextMuted}" FontSize="10" Margin="0,0,0,5" />
                  <ProgressBar x:Name="UnpackProgress" Height="8" Background="{StaticResource BgMid}" Foreground="{StaticResource PurpleAccent}" BorderThickness="0" Margin="0,0,0,8" Visibility="Collapsed" />
                  <Button Content="🔍 預覽分析" Style="{StaticResource PurpleButtonStyle}" Height="28" Margin="0,0,0,6" Click="Preview_Click" />
                  <Button Content="🚀 開始拆分" Style="{StaticResource PurpleButtonStyle}" Height="28" Click="Unpack_Click" />
              </StackPanel>
          </Grid>

          <GridSplitter Grid.Column="1" Width="5" Background="{DynamicResource BorderColor}" HorizontalAlignment="Stretch" />

          <!-- 2. 中間切片清單欄 -->
          <Grid Grid.Column="2" Margin="5,0">
              <Grid.RowDefinitions>
                  <RowDefinition Height="Auto" />
                  <RowDefinition Height="*" />
              </Grid.RowDefinitions>
              <TextBlock Grid.Row="0" Text="📋 切片清單" Style="{StaticResource SectionLabelStyle}" Margin="0,0,0,5" />
              <ListBox Grid.Row="1" x:Name="SliceListBox" Background="{StaticResource BgMid}" BorderThickness="0" SelectionChanged="SliceListBox_SelectionChanged">
                  <ListBox.ItemContainerStyle>
                      <Style TargetType="ListBoxItem">
                          <Setter Property="Background" Value="Transparent" />
                          <Setter Property="Foreground" Value="{StaticResource TextPrimary}" />
                          <Setter Property="Padding" Value="4" />
                          <Setter Property="Template">
                              <Setter.Value>
                                  <ControlTemplate TargetType="ListBoxItem">
                                      <Border Background="{TemplateBinding Background}" BorderBrush="{StaticResource BorderColor}" BorderThickness="0,0,0,1" Padding="4">
                                          <ContentPresenter />
                                      </Border>
                                      <ControlTemplate.Triggers>
                                          <Trigger Property="IsSelected" Value="True">
                                              <Setter Property="Background" Value="{StaticResource PurpleSelection}" />
                                          </Trigger>
                                      </ControlTemplate.Triggers>
                                  </ControlTemplate>
                              </Setter.Value>
                          </Setter>
                      </Style>
                  </ListBox.ItemContainerStyle>
                  <ListBox.ItemTemplate>
                      <DataTemplate>
                          <Grid Margin="2">
                              <Grid.ColumnDefinitions>
                                  <ColumnDefinition Width="36" />
                                  <ColumnDefinition Width="*" />
                              </Grid.ColumnDefinitions>
                              <Border Width="32" Height="32" BorderBrush="{StaticResource BorderColor}" BorderThickness="1" Background="{StaticResource BgDark}">
                                  <Image Source="{Binding Thumbnail}" Stretch="Uniform" RenderOptions.BitmapScalingMode="NearestNeighbor" />
                              </Border>
                              <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="6,0,0,0">
                                  <TextBlock Text="{Binding Name}" FontWeight="Bold" FontSize="10" TextTrimming="CharacterEllipsis" />
                                  <TextBlock Foreground="{StaticResource TextMuted}" FontSize="9">
                                      <Run Text="{Binding Width, Mode=OneWay}" /><Run Text="x" /><Run Text="{Binding Height, Mode=OneWay}" />
                                  </TextBlock>
                              </StackPanel>
                          </Grid>
                      </DataTemplate>
                  </ListBox.ItemTemplate>
              </ListBox>
          </Grid>

          <GridSplitter Grid.Column="3" Width="5" Background="{DynamicResource BorderColor}" HorizontalAlignment="Stretch" />

          <!-- 3. 右側大圖預覽欄 -->
          <Grid Grid.Column="4" Margin="5,0,0,0">
              <Grid.RowDefinitions>
                  <RowDefinition Height="Auto" />
                  <RowDefinition Height="*" />
              </Grid.RowDefinitions>

              <TextBlock Grid.Row="0" Text="🖼 合圖預覽 (點選左欄項目以高亮顯示)" Style="{StaticResource SectionLabelStyle}" Margin="0,0,0,5" />
              
              <ScrollViewer Grid.Row="1" x:Name="PreviewScroll" HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
                  <Grid>
                      <Grid.Background>
                          <DrawingBrush TileMode="Tile" Viewport="0,0,16,16" ViewportUnits="Absolute">
                              <DrawingBrush.Drawing>
                                  <DrawingGroup>
                                      <GeometryDrawing Brush="#1A1A2E" Geometry="M0,0 H16 V16 H0Z" />
                                      <GeometryDrawing Brush="#12121E" Geometry="M0,0 H8 V8 H0Z M8,8 H16 V16 H8Z" />
                                  </DrawingGroup>
                              </DrawingBrush.Drawing>
                          </DrawingBrush>
                      </Grid.Background>

                      <Grid x:Name="ImageContainer" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16">
                          <Image x:Name="PreviewImage" Stretch="None" RenderOptions.BitmapScalingMode="NearestNeighbor" />
                          <Canvas x:Name="HighlightCanvas" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" IsHitTestVisible="False" />
                      </Grid>
                  </Grid>
              </ScrollViewer>
          </Grid>
      </Grid>
  </UserControl>
  ```

- [ ] **Step 2: 建立 UnpackerControl Code-Behind**
  建立 `src/AtlasForge/Views/Controls/UnpackerControl.xaml.cs`，內容為原 `UnpackWindow.xaml.cs` 的核心邏輯，但將 `dialog.ShowDialog(this)` 改成獲取當前 Parent Window 或直接執行 `dialog.ShowDialog(Window.GetWindow(this))`。並做好 TextChanged 等防呆空值判斷：
  ```csharp
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

  namespace AtlasForge.Views.Controls;

  public partial class UnpackerControl : UserControl
  {
      private readonly UnpackService _unpackService = new();
      private BitmapImage? _loadedImage;
      private List<SliceItem> _slices = new();
      private double _zoom = 1.0;

      public UnpackerControl()
      {
          InitializeComponent();
          PreviewScroll.PreviewMouseWheel += PreviewScroll_MouseWheel;
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
  ```

- [ ] **Step 3: 執行驗證建置**
  執行: `dotnet build AtlasForge.sln`
  確認無編譯錯誤。

- [ ] **Step 4: Commit 新增檔案**
  ```bash
  git add src/AtlasForge/Views/Controls/UnpackerControl.xaml src/AtlasForge/Views/Controls/UnpackerControl.xaml.cs
  git commit -m "feat(unpack): create UnpackerControl UserControl"
  ```

---

### Task 3: 在 MainWindow 中整合 UnpackerControl 與分頁

**Files:**
- Modify: `src/AtlasForge/Views/MainWindow.xaml`
- Modify: `src/AtlasForge/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 修改 MainWindow.xaml 介面配置**
  調整 `src/AtlasForge/Views/MainWindow.xaml`：
  1. 在工具列最左側增加兩個模式按鈕：「合圖打包」與「序列拆圖」。
  2. 將打包模式相關按鈕放在一個 `StackPanel` 中，由 `IsPackerMode` (Visibility) 控制其顯示。
  3. 修改下方主要區域，放入兩個視窗容器：
     - Packer Layout (包含 `FrameListControl`, `GridSplitter`, `AtlasPreviewControl`, `SettingsPanel`) 放入第一個 Grid。
     - Unpacker Layout (放入 `ctrl:UnpackerControl`) 放入第二個 Grid。
     - 二者藉由 visibility 切換。
  
  ```xml
  <!-- 更新後的 MainWindow.xaml 內容 -->
  <Window x:Class="AtlasForge.Views.MainWindow"
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ctrl="clr-namespace:AtlasForge.Views.Controls"
          xmlns:vm="clr-namespace:AtlasForge.ViewModels"
          Title="AtlasForge"
          Height="720"
          Width="1100"
          MinWidth="900"
          MinHeight="600"
          Background="{StaticResource BgDark}">
      <Window.DataContext>
          <vm:MainViewModel />
      </Window.DataContext>
      <Window.InputBindings>
          <KeyBinding Key="E" Modifiers="Ctrl" Command="{Binding ExportCommand}" />
          <KeyBinding Key="Delete" Command="{Binding RemoveSelectedFrameCommand}" />
      </Window.InputBindings>

      <DockPanel>
          <Border DockPanel.Dock="Top"
                  Background="{StaticResource BgMid}"
                  BorderBrush="{DynamicResource BorderColor}"
                  BorderThickness="0,0,0,1"
                  Height="36">
              <DockPanel VerticalAlignment="Center"
                         Margin="12,0"
                         LastChildFill="False">
                  <Button DockPanel.Dock="Right"
                          Style="{StaticResource UpdateBadgeStyle}"
                          Content="{Binding UpdateBadgeText}"
                          Command="{Binding OpenUpdateCommand}"
                          Visibility="{Binding HasUpdate, Converter={StaticResource BoolToVis}}" />
                  
                  <TextBlock DockPanel.Dock="Left"
                             x:Name="TitleLbl"
                             Text="⚡ AtlasForge"
                             Foreground="#38BDF8"
                             FontWeight="Bold"
                             FontSize="13"
                             VerticalAlignment="Center" />
                  
                  <Separator DockPanel.Dock="Left"
                             Width="1"
                             Background="{StaticResource BgLight}"
                             Margin="12,6" />

                  <!-- 模式頁籤選擇器 -->
                  <Button x:Name="PackerTabBtn"
                          DockPanel.Dock="Left"
                          Content="⚡ 合圖打包"
                          Style="{StaticResource TabActiveStyle}"
                          Margin="0,0,4,0"
                          Click="PackerTab_Click" />
                  <Button x:Name="UnpackerTabBtn"
                          DockPanel.Dock="Left"
                          Content="🔧 序列拆圖"
                          Style="{StaticResource TabInactiveStyle}"
                          Margin="0,0,4,0"
                          Click="UnpackerTab_Click" />

                  <Separator DockPanel.Dock="Left"
                             Width="1"
                             Background="{StaticResource BgLight}"
                             Margin="12,6" />

                  <!-- 打包模式工具列 (在拆圖模式下會隱藏) -->
                  <StackPanel x:Name="PackerToolbar" DockPanel.Dock="Left" Orientation="Horizontal" Visibility="Visible">
                      <Button Content="📂 開啟資料夾"
                              Style="{StaticResource ToolbarButtonStyle}"
                              Margin="0,0,4,0"
                              Click="OpenFolder_Click" />
                      <Button Content="📄 選擇檔案"
                              Style="{StaticResource ToolbarButtonStyle}"
                              Margin="0,0,4,0"
                              Click="OpenFiles_Click" />
                      <Button Content="🗑 清除"
                              Style="{StaticResource ToolbarButtonStyle}"
                              Command="{Binding ClearFramesCommand}" />
                  </StackPanel>
              </DockPanel>
          </Border>

          <Grid>
              <!-- Packer 模式主要介面 -->
              <Grid x:Name="PackerMainGrid" Visibility="Visible">
                  <Grid.ColumnDefinitions>
                      <Grid.ColumnDefinition Width="190" MinWidth="140" />
                      <Grid.ColumnDefinition Width="5" />
                      <Grid.ColumnDefinition Width="*" MinWidth="300" />
                      <Grid.ColumnDefinition Width="5" />
                      <Grid.ColumnDefinition Width="185" MinWidth="160" />
                  </Grid.ColumnDefinitions>

                  <ctrl:FrameListControl Grid.Column="0" DataContext="{Binding}" />
                  <GridSplitter Grid.Column="1"
                                Width="5"
                                Background="{DynamicResource BorderColor}"
                                HorizontalAlignment="Stretch" />
                  <ctrl:AtlasPreviewControl Grid.Column="2" DataContext="{Binding}" />
                  <GridSplitter Grid.Column="3"
                                Width="5"
                                Background="{DynamicResource BorderColor}"
                                HorizontalAlignment="Stretch" />
                  <ctrl:SettingsPanel Grid.Column="4" DataContext="{Binding}" />
              </Grid>

              <!-- Unpacker 模式主要介面 -->
              <ctrl:UnpackerControl x:Name="UnpackerMainGrid" Visibility="Collapsed" />
          </Grid>
      </DockPanel>
  </Window>
  ```

- [ ] **Step 2: 修改 MainWindow.xaml.cs 邏輯**
  修改 `src/AtlasForge/Views/MainWindow.xaml.cs`，處理切換分頁時：
  1. 更新 `PackerMainGrid` 與 `UnpackerMainGrid` 的 Visibility。
  2. 更新頂部 Tab 按鈕的 Style。
  3. 更新 Packer 工具列的 Visibility。
  4. 變更 `Title`（加入 `(拆圖模式)` 後綴與變更前綴 Foreground 顏色）。
  5. 變更視窗全域 `Resources` 中 `Accent`、`AccentHover`、`BorderColor` 的 Brush 值，使全域顏色變更。

  ```csharp
  using System.IO;
  using System.Windows;
  using System.Windows.Input;
  using System.Windows.Media;
  using AtlasForge.ViewModels;

  namespace AtlasForge.Views;

  public partial class MainWindow : System.Windows.Window
  {
      private bool _isUnpackerMode;

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

      private async void OpenFiles_Click(object _, System.Windows.RoutedEventArgs e)
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

      private void PackerTab_Click(object _, RoutedEventArgs e)
      {
          SwitchToMode(false);
      }

      private void UnpackerTab_Click(object _, RoutedEventArgs e)
      {
          SwitchToMode(true);
      }

      private void SwitchToMode(bool isUnpacker)
      {
          _isUnpackerMode = isUnpacker;

          if (isUnpacker)
          {
              // 切換至拆圖模式
              PackerMainGrid.Visibility = Visibility.Collapsed;
              UnpackerMainGrid.Visibility = Visibility.Visible;
              PackerToolbar.Visibility = Visibility.Collapsed;

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
              PackerMainGrid.Visibility = Visibility.Visible;
              UnpackerMainGrid.Visibility = Visibility.Collapsed;
              PackerToolbar.Visibility = Visibility.Visible;

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
  ```

- [ ] **Step 3: 執行建置驗證**
  執行: `dotnet build AtlasForge.sln`
  確認沒有編譯錯誤。

- [ ] **Step 4: Commit 整合**
  ```bash
  git add src/AtlasForge/Views/MainWindow.xaml src/AtlasForge/Views/MainWindow.xaml.cs
  git commit -m "feat(unpack): integrate UnpackerControl and dynamic theme switching to MainWindow"
  ```

---

### Task 4: 清除舊獨立視窗並跑 CI 驗證

**Files:**
- Delete: `src/AtlasForge/Views/UnpackWindow.xaml`
- Delete: `src/AtlasForge/Views/UnpackWindow.xaml.cs`

- [ ] **Step 1: 刪除舊檔案**
  刪除 `src/AtlasForge/Views/UnpackWindow.xaml` 與 `src/AtlasForge/Views/UnpackWindow.xaml.cs`。

- [ ] **Step 2: 執行完整的 dotnet format 驗證**
  執行格式檢查，確保 CI 門檻（強制無變更）：
  `dotnet format AtlasForge.sln --verify-no-changes`
  若有格式錯誤，則執行 `dotnet format AtlasForge.sln` 修復後重新驗證。

- [ ] **Step 3: 執行單元測試**
  `dotnet test AtlasForge.sln`
  確認所有測試通過。

- [ ] **Step 4: Commit 清理與完成**
  ```bash
  git rm src/AtlasForge/Views/UnpackWindow.xaml src/AtlasForge/Views/UnpackWindow.xaml.cs
  git commit -m "cleanup(unpack): remove unused UnpackWindow files"
  ```

## Verification Plan

### Automated Tests
- 執行 `dotnet test AtlasForge.sln` 確保沒有 Regression。

### Manual Verification
1. 執行 `dotnet run --project src/AtlasForge/AtlasForge.csproj` 開啟應用程式。
2. 預設顯示「⚡ 合圖打包」，畫面的主要顏色為「科技藍」。
3. 點擊頂部「🔧 序列拆圖」，畫面會順暢切換為拆圖面板，且主按鈕、頁籤外框等顏色立刻變為「科技紫」，標題加上 `(拆圖模式)`。
4. 於拆圖面板載入 PNG，以「網格 4x4」進行預覽，檢查是否可正確取得縮圖且切片高亮正確為「科技紫」。
5. 點擊「⚡ 合圖打包」返回，確認畫面恢復為打包模式且顏色變回「科技藍」。
