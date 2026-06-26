# 序列圖拆分/解包工具實作計畫 (Texture Unpacker Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 實作一個獨立的「拆圖工具 (Texture Unpacker)」視窗，支援透過 JSON/Plist 描述檔或網格（Grid）分析合圖並切片，顯示帶有子圖縮圖的清單，並支援點擊高亮標示與專屬的「科技紫」配色主題。

**Architecture:**
1. **Model & Service**: 建立 `SliceItem` 與 `UnpackService`。前者記錄切片詳細尺寸、還原資訊與 `ImageSource` 縮圖；後者負責解析 JSON/Plist（還原透明畫布裁切）與網格計算，以及最後的實體圖片輸出。
2. **View & Interaction**: 建立 `UnpackWindow` (三欄式 WPF 視窗)。在控制面板、清單選取背景、高亮選取框上使用科技紫主題色 (`#8B5CF6`)。使用 `CroppedBitmap` 從合圖直接裁剪縮圖。
3. **Integration**: 主畫面工具列新增「🔧 拆圖工具」按鈕，點擊開啟此視窗。

**Tech Stack:** WPF / C# 12 / .NET 8 / SkiaSharp，無額外依賴。

## Global Constraints
- C# nullable enable，implicit usings
- 事件處理常式中以 `_` 捨棄未使用的 `sender`
- UI 文字繁體中文
- 變數命名規範：私有欄位底線 camelCase，靜態唯讀/常數 PascalCase
- 每次 Commit 前需跑完 `dotnet format`

---

### Task 1: 建立 SliceItem 模型與 UnpackService 核心服務

**Files:**
- Create: `src/AtlasForge/Models/SliceItem.cs`
- Create: `src/AtlasForge/Services/UnpackService.cs`
- Create: `tests/AtlasForge.Tests/Services/UnpackServiceTests.cs`

- [ ] **Step 1: 建立 SliceItem 資料型別**
  建立 `src/AtlasForge/Models/SliceItem.cs`，定義檔案結構：
  ```csharp
  using System.Windows.Media;

  namespace AtlasForge.Models;

  public class SliceItem
  {
      public string Name { get; set; } = "";
      public int X { get; set; }
      public int Y { get; set; }
      public int Width { get; set; }
      public int Height { get; set; }
      public int OffsetX { get; set; }
      public int OffsetY { get; set; }
      public int SourceWidth { get; set; }
      public int SourceHeight { get; set; }
      public ImageSource? Thumbnail { get; set; }
  }
  ```

- [ ] **Step 2: 建立 UnpackService 並實作 JSON 解析**
  建立 `src/AtlasForge/Services/UnpackService.cs`，使用 `System.Text.Json` 解析打包的 JSON (Hash 格式)：
  ```csharp
  using System.IO;
  using System.Text.Json;
  using System.Windows;
  using System.Windows.Media.Imaging;
  using AtlasForge.Models;
  using SkiaSharp;

  namespace AtlasForge.Services;

  public class UnpackService
  {
      public List<SliceItem> ParseJson(string jsonPath, BitmapSource atlasSource)
      {
          var list = new List<SliceItem>();
          using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
          var root = doc.RootElement;
          if (!root.TryGetProperty("frames", out var framesProp))
          {
              return list;
          }

          foreach (var frameObj in framesProp.EnumerateObject())
          {
              var name = frameObj.Name;
              var val = frameObj.Value;
              var frame = val.GetProperty("frame");
              var spriteSourceSize = val.GetProperty("spriteSourceSize");
              var sourceSize = val.GetProperty("sourceSize");

              var item = new SliceItem
              {
                  Name = Path.GetFileNameWithoutExtension(name),
                  X = frame.GetProperty("x").GetInt32(),
                  Y = frame.GetProperty("y").GetInt32(),
                  Width = frame.GetProperty("w").GetInt32(),
                  Height = frame.GetProperty("h").GetInt32(),
                  OffsetX = spriteSourceSize.GetProperty("x").GetInt32(),
                  OffsetY = spriteSourceSize.GetProperty("y").GetInt32(),
                  SourceWidth = sourceSize.GetProperty("w").GetInt32(),
                  SourceHeight = sourceSize.GetProperty("h").GetInt32()
              };

              item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height);
              list.Add(item);
          }
          return list;
      }

      private static BitmapSource CreateThumbnail(BitmapSource source, int x, int y, int w, int h)
      {
          if (x < 0 || y < 0 || x + w > source.PixelWidth || y + h > source.PixelHeight || w <= 0 || h <= 0)
          {
              return new CroppedBitmap(source, new Int32Rect(0, 0, 1, 1));
          }
          return new CroppedBitmap(source, new Int32Rect(x, y, w, h));
      }
  }
  ```

- [ ] **Step 3: 實作 Plist 解析**
  在 `UnpackService.cs` 中加入 `ParsePlist` 邏輯，使用 `System.Xml.Linq`：
  ```csharp
  using System.Xml.Linq;

  public List<SliceItem> ParsePlist(string plistPath, BitmapSource atlasSource)
  {
      var list = new List<SliceItem>();
      var doc = XDocument.Load(plistPath);
      var plistDict = doc.Element("plist")?.Element("dict");
      if (plistDict is null)
      {
          return list;
      }

      var framesKey = plistDict.Elements("key").FirstOrDefault(k => k.Value == "frames");
      if (framesKey?.NextNode is not XElement framesDict || framesDict.Name != "dict")
      {
          return list;
      }

      var keys = framesDict.Elements("key").ToList();
      var dicts = framesDict.Elements("dict").ToList();

      for (var i = 0; i < keys.Count; i++)
      {
          var name = keys[i].Value;
          var frameDict = dicts[i];

          var keysInFrame = frameDict.Elements("key").ToList();
          var frameStr = "";
          var offsetStr = "";
          var sourceSizeStr = "";

          for (var j = 0; j < keysInFrame.Count; j++)
          {
              var k = keysInFrame[j].Value;
              var v = keysInFrame[j].NextNode as XElement;
              if (v is null) continue;

              if (k == "frame") frameStr = v.Value;
              else if (k == "offset") offsetStr = v.Value;
              else if (k == "sourceSize") sourceSizeStr = v.Value;
          }

          var (fx, fy, fw, fh) = ParseRect(frameStr);
          var (ox, oy) = ParsePoint(offsetStr);
          var (sw, sh) = ParsePoint(sourceSizeStr);

          // cocos2d offset 座標系是中心點偏移量，轉換成左上角原點偏移量：
          var leftTopX = (sw - fw) / 2 + ox;
          var leftTopY = (sh - fh) / 2 - oy; // plist 通常 Y 軸向上

          var item = new SliceItem
          {
              Name = Path.GetFileNameWithoutExtension(name),
              X = fx,
              Y = fy,
              Width = fw,
              Height = fh,
              OffsetX = Math.Max(0, leftTopX),
              OffsetY = Math.Max(0, leftTopY),
              SourceWidth = sw,
              SourceHeight = sh
          };

          item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height);
          list.Add(item);
      }

      return list;
  }

  private static (int X, int Y) ParsePoint(string s)
  {
      var parts = s.Trim('{', '}').Split(',');
      return (int.Parse(parts[0]), int.Parse(parts[1]));
  }

  private static (int X, int Y, int W, int H) ParseRect(string s)
  {
      var cleaned = s.Replace(" ", "").Replace("{{", "").Replace("}}", "");
      var parts = cleaned.Split(new[] { "},{" }, StringSplitOptions.None);
      var ptParts = parts[0].Split(',');
      var szParts = parts[1].Split(',');
      return (int.Parse(ptParts[0]), int.Parse(ptParts[1]), int.Parse(szParts[0]), int.Parse(szParts[1]));
  }
  ```

- [ ] **Step 4: 實作網格切片計算與實體導出**
  在 `UnpackService.cs` 中實作 `GenerateGridSlices` 與 `SaveSlices`：
  ```csharp
  public List<SliceItem> GenerateGridSlices(int imgWidth, int imgHeight, int cols, int rows, BitmapSource atlasSource)
  {
      var list = new List<SliceItem>();
      var cellW = imgWidth / cols;
      var cellH = imgHeight / rows;

      for (var r = 0; r < rows; r++)
      {
          for (var c = 0; c < cols; c++)
          {
              var index = r * cols + c;
              var item = new SliceItem
              {
                  Name = $"frame_{index}",
                  X = c * cellW,
                  Y = r * cellH,
                  Width = cellW,
                  Height = cellH,
                  OffsetX = 0,
                  OffsetY = 0,
                  SourceWidth = cellW,
                  SourceHeight = cellH
              };
              item.Thumbnail = CreateThumbnail(atlasSource, item.X, item.Y, item.Width, item.Height);
              list.Add(item);
          }
      }
      return list;
  }

  public void SaveSlices(string atlasImagePath, List<SliceItem> slices, string outputDir)
  {
      Directory.CreateDirectory(outputDir);
      using var atlas = SKBitmap.Decode(atlasImagePath);
      if (atlas is null)
      {
          throw new InvalidOperationException("無法載入合圖點陣圖。");
      }

      foreach (var slice in slices)
      {
          using var restored = new SKBitmap(slice.SourceWidth, slice.SourceHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
          using (var canvas = new SKCanvas(restored))
          {
              canvas.Clear(SKColors.Transparent);
              var srcRect = SKRect.Create(slice.X, slice.Y, slice.Width, slice.Height);
              var destRect = SKRect.Create(slice.OffsetX, slice.OffsetY, slice.Width, slice.Height);
              canvas.DrawBitmap(atlas, srcRect, destRect);
          }

          var outPath = Path.Combine(outputDir, $"{slice.Name}.png");
          using var image = SKImage.FromBitmap(restored);
          using var data = image.Encode(SKEncodedImageFormat.Png, 100);
          using var stream = File.OpenWrite(outPath);
          data.SaveTo(stream);
      }
  }
  ```

- [ ] **Step 5: 撰寫 UnpackService 單元測試**
  建立 `tests/AtlasForge.Tests/Services/UnpackServiceTests.cs` :
  ```csharp
  using AtlasForge.Services;
  using Xunit;

  namespace AtlasForge.Tests.Services;

  public class UnpackServiceTests
  {
      [Fact]
      public void GenerateGridSlices_ReturnsCorrectCounts()
      {
          var service = new UnpackService();
          // 用無圖的 Null source 測試計算
          var slices = service.GenerateGridSlices(200, 200, 4, 4, null!);
          Assert.Equal(16, slices.Count);
          Assert.Equal(50, slices[0].Width);
          Assert.Equal(50, slices[0].Height);
          Assert.Equal(50, slices[5].X); // 第二列第二行應在 (50, 50)
      }
  }
  ```

- [ ] **Step 6: 驗證建置狀況**
  本機建置以確認無編譯問題。

- [ ] **Step 7: Commit**
  ```bash
  git add src/AtlasForge/Models/SliceItem.cs src/AtlasForge/Services/UnpackService.cs tests/AtlasForge.Tests/Services/UnpackServiceTests.cs
  git commit -m "feat(unpack): add SliceItem model and UnpackService with test"
  ```

---

### Task 2: 建立 UnpackWindow XAML 配置 (科技紫主題)

**Files:**
- Create: `src/AtlasForge/Views/UnpackWindow.xaml`

- [ ] **Step 1: 建立 UnpackWindow XAML**
  建立 `src/AtlasForge/Views/UnpackWindow.xaml` 視窗佈局，使用三欄設計與科技紫主題色資源：
  ```xml
  <Window x:Class="AtlasForge.Views.UnpackWindow"
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          Title="拆圖工具 (Texture Unpacker)"
          Width="860" Height="500"
          MinWidth="700" MinHeight="400"
          WindowStartupLocation="CenterOwner"
          Background="{StaticResource BgDark}">
      <Window.Resources>
          <!-- 科技紫主題配色 -->
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
      </Window.Resources>

      <Grid Margin="10">
          <Grid.ColumnDefinitions>
              <!-- 1. 控制欄 -->
              <ColumnDefinition Width="240" />
              <ColumnDefinition Width="5" />
              <!-- 2. 切片清單欄 -->
              <ColumnDefinition Width="180" />
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
                          <StackPanel>
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

          <GridSplitter Grid.Column="1" Width="5" Background="{StaticResource BorderColor}" HorizontalAlignment="Stretch" />

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
                                  <TextBlock Text="{Binding Width, StringFormat={}{0}x}{Binding Height}" Foreground="{StaticResource TextMuted}" FontSize="9" />
                              </StackPanel>
                          </Grid>
                      </DataTemplate>
                  </ListBox.ItemTemplate>
              </ListBox>
          </Grid>

          <GridSplitter Grid.Column="3" Width="5" Background="{StaticResource BorderColor}" HorizontalAlignment="Stretch" />

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
  </Window>
  ```

- [ ] **Step 2: Commit**
  ```bash
  git add src/AtlasForge/Views/UnpackWindow.xaml
  git commit -m "style: create UnpackWindow XAML layout with purple theme"
  ```

---

### Task 3: 實作 UnpackWindow Code-Behind 邏輯

**Files:**
- Create: `src/AtlasForge/Views/UnpackWindow.xaml.cs`

- [ ] **Step 1: 建立 Code-Behind 架構與基礎欄位**
  建立 `src/AtlasForge/Views/UnpackWindow.xaml.cs`，匯入命名空間，設定成員變數：
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

  namespace AtlasForge.Views;

  public partial class UnpackWindow : Window
  {
      private readonly UnpackService _unpackService = new();
      private BitmapImage? _loadedImage;
      private List<SliceItem> _slices = new();

      public UnpackWindow()
      {
          InitializeComponent();
          PreviewScroll.PreviewMouseWheel += PreviewScroll_MouseWheel;
      }
  }
  ```

- [ ] **Step 2: 實作瀏覽與拖放檔案 handler**
  ```csharp
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

      private void Txt_DragOver(object _, DragEventArgs e)
      {
          e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
          e.Handled = true;
      }

      private void Image_Drop(object _, DragEventArgs e)
      {
          if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && files[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase))
          {
              LoadImage(files[0]);
          }
      }

      private void Desc_Drop(object _, DragEventArgs e)
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
  ```

- [ ] **Step 3: 實作載入圖片與預覽分析**
  ```csharp
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
          if (DescPanel is null || GridPanel is null) return;
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
          // 清除舊清單
          SliceListBox.ItemsSource = null;
          HighlightCanvas.Children.Clear();
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
  ```

- [ ] **Step 4: 實作清單選取高亮與預覽滾輪縮放**
  ```csharp
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

      private double _zoom = 1.0;
      private void PreviewScroll_MouseWheel(object _, MouseWheelEventArgs e)
      {
          if (Keyboard.Modifiers != ModifierKeys.Control) return;
          e.Handled = true;
          var factor = e.Delta > 0 ? 1.25 : 1.0 / 1.25;
          _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
          ImageContainer.LayoutTransform = new ScaleTransform(_zoom, _zoom);
      }
  ```

- [ ] **Step 5: 實作開始拆分非同步儲存**
  ```csharp
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
  ```

- [ ] **Step 6: 驗證建置狀況**
  本機進行 `dotnet build` 檢查。

- [ ] **Step 7: Commit**
  ```bash
  git add src/AtlasForge/Views/UnpackWindow.xaml.cs
  git commit -m "feat(unpack): implement UnpackWindow code-behind logic"
  ```

---

### Task 4: 與主視窗整合

**Files:**
- Modify: `src/AtlasForge/Views/MainWindow.xaml`
- Modify: `src/AtlasForge/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 主視窗工具列新增按鈕**
  在 `src/AtlasForge/Views/MainWindow.xaml` 中，於頂部工具列「選擇檔案」與「清除」按鈕旁，加一個分隔並加入「🔧 拆圖工具」按鈕：
  尋找：
  ```xml
                  <Button DockPanel.Dock="Left"
                          Content="🗑 清除"
                          Style="{StaticResource ToolbarButtonStyle}"
                          Command="{Binding ClearFramesCommand}" />
  ```
  在下方加入：
  ```xml
                  <Border Width="1" Height="16" Background="{StaticResource BorderColor}" Margin="8,0" DockPanel.Dock="Left"/>
                  <Button DockPanel.Dock="Left"
                          Content="🔧 拆圖工具"
                          Style="{StaticResource ToolbarButtonStyle}"
                          Click="OpenUnpacker_Click" />
  ```

- [ ] **Step 2: 實作 Click 開啟視窗**
  在 `src/AtlasForge/Views/MainWindow.xaml.cs` 中實作 `OpenUnpacker_Click` 處理常式：
  ```csharp
      private void OpenUnpacker_Click(object _, RoutedEventArgs e)
      {
          var unpacker = new UnpackWindow { Owner = this };
          unpacker.ShowDialog();
      }
  ```

- [ ] **Step 3: 驗證編譯與程式碼排版**
  在終端機執行檢查：
  ```bash
  dotnet format AtlasForge.sln --verify-no-changes
  dotnet build AtlasForge.sln
  ```
  預期：建置成功，且 0 格式警告、0 編譯錯誤。

- [ ] **Step 4: Commit**
  ```bash
  git add src/AtlasForge/Views/MainWindow.xaml src/AtlasForge/Views/MainWindow.xaml.cs
  git commit -m "feat(unpack): integrate UnpackWindow into MainWindow toolbar"
  ```
