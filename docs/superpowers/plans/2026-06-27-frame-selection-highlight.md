# 預覽畫面選取幀高亮標示實作計畫 (Frame Selection Highlight Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 當點選左側序列圖時，於右側預覽畫面（Atlas 模式）中高亮標示對應的圖片區塊。

**Architecture:** 在 `AtlasPreviewControl` 中使用 `Grid`（`ImageContainer`）包裝 `PreviewImage`，並放置一個 `Canvas` 作為覆蓋層。在 `AtlasPreviewControl` 的 code-behind 中監聽 `MainViewModel` 的 `PropertyChanged` 事件。當 `SelectedFrame` 改變且在 Atlas 模式時，利用對應的 `SpriteRect` 座標在 `Canvas` 上動態繪製一個帶有藍色主題外框與 15% 透明藍色填滿的 `Border`。

**Tech Stack:** WPF / C# 12 / .NET 8，無額外依賴。

## Global Constraints
- C# nullable enable，implicit usings
- 事件處理常式中使用 `_` 捨棄未使用的 `sender`
- UI 文字繁體中文
- 私有靜態唯讀欄位與常數應採用 PascalCase，一般私有變數採用 camelCase 且以底線開頭（例如：`_zoom`）
- `dotnet format` 在 commit 前跑完以符合規範

---

### Task 1: 預覽畫面 XAML 結構調整

**Files:**
- Modify: `src/AtlasForge/Views/Controls/AtlasPreviewControl.xaml`

- [ ] **Step 1: 將 PreviewImage 包裹進 Grid 容器，並加入 Canvas 覆蓋層**
  將 `PreviewImage` 改為如下結構，並加入 `ImageContainer` 與 `HighlightCanvas`：
  ```xml
  <!-- 將 PreviewImage 包裹進 Grid -->
  <Grid x:Name="ImageContainer"
        HorizontalAlignment="Center"
        VerticalAlignment="Center"
        Margin="16">
      <Image x:Name="PreviewImage"
             Stretch="None"
             Source="{Binding AtlasPreview}"
             RenderOptions.BitmapScalingMode="NearestNeighbor" />
      <Canvas x:Name="HighlightCanvas"
              HorizontalAlignment="Stretch"
              VerticalAlignment="Stretch"
              IsHitTestVisible="False" />
  </Grid>
  ```

- [ ] **Step 2: 驗證 XAML 的建置狀況**
  預期本步驟無編譯錯誤。
  
- [ ] **Step 3: Commit**
  ```bash
  git add src/AtlasForge/Views/Controls/AtlasPreviewControl.xaml
  git commit -m "style: wrap PreviewImage in Grid container and add HighlightCanvas"
  ```

---

### Task 2: Code-Behind 控制與高亮標示邏輯

**Files:**
- Modify: `src/AtlasForge/Views/Controls/AtlasPreviewControl.xaml.cs`

- [ ] **Step 1: 變更縮放 LayoutTransform 目標**
  將原本作用在 `PreviewImage` 上的縮放，改作用在整個外層容器 `ImageContainer` 上：
  ```csharp
  private void SetZoom(double zoom)
  {
      _zoom = Math.Clamp(zoom, 0.1, 8.0);
      ImageContainer.LayoutTransform = new ScaleTransform(_zoom, _zoom);
      ZoomLabel.Text = $"{_zoom:P0}";
  }
  ```

- [ ] **Step 2: 加入監聽 DataContext PropertyChanged 機制**
  在建構子中註冊 `DataContextChanged`，並當 `SelectedFrame`、`CurrentAtlas` 或 `AtlasPreview` 改變時觸發 `UpdateHighlight()`：
  ```csharp
  public AtlasPreviewControl()
  {
      InitializeComponent();
      DataContextChanged += AtlasPreviewControl_DataContextChanged;
  }

  private void AtlasPreviewControl_DataContextChanged(object _, DependencyPropertyChangedEventArgs e)
  {
      if (e.OldValue is MainViewModel oldVm)
      {
          oldVm.PropertyChanged -= VM_PropertyChanged;
      }
      if (e.NewValue is MainViewModel newVm)
      {
          newVm.PropertyChanged += VM_PropertyChanged;
      }
      UpdateHighlight();
  }

  private void VM_PropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
  {
      if (e.PropertyName == nameof(MainViewModel.SelectedFrame) ||
          e.PropertyName == nameof(MainViewModel.CurrentAtlas) ||
          e.PropertyName == nameof(MainViewModel.AtlasPreview))
      {
          UpdateHighlight();
      }
  }
  ```

- [ ] **Step 3: 切換預覽分頁時同步更新高亮狀態**
  在分頁點擊處理常式末端呼叫 `UpdateHighlight()`：
  ```csharp
  private void AtlasTab_Click(object _, RoutedEventArgs e)
  {
      VM?.ShowAtlasView();
      AtlasTabBtn.Style = (Style)FindResource("TabActiveStyle");
      AnimTabBtn.Style = (Style)FindResource("TabInactiveStyle");
      UpdateHighlight();
  }

  private void AnimTab_Click(object _, RoutedEventArgs e)
  {
      AnimTabBtn.Style = (Style)FindResource("TabActiveStyle");
      AtlasTabBtn.Style = (Style)FindResource("TabInactiveStyle");
      VM?.ToggleAnimationCommand.Execute(null);
      UpdateHighlight();
  }
  ```

- [ ] **Step 4: 實作高亮框線繪製方法 UpdateHighlight**
  定義 `IsAtlasMode` 輔助屬性，並實作 `UpdateHighlight`，動態在 `HighlightCanvas` 上產生 `Border` 元素：
  ```csharp
  private bool IsAtlasMode => AtlasTabBtn != null && AtlasTabBtn.Style == (Style)FindResource("TabActiveStyle");

  private void UpdateHighlight()
  {
      HighlightCanvas.Children.Clear();

      if (VM is null || VM.SelectedFrame is null || VM.CurrentAtlas is null || !IsAtlasMode)
      {
          return;
      }

      var index = VM.SelectedFrame.OrderIndex;
      if (index < 0 || index >= VM.CurrentAtlas.Frames.Count)
      {
          return;
      }

      var rect = VM.CurrentAtlas.Frames[index];

      var highlight = new System.Windows.Controls.Border
      {
          Width = rect.Width,
          Height = rect.Height,
          BorderThickness = new Thickness(2),
          BorderBrush = (System.Windows.Media.Brush)FindResource("Accent") ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)),
          Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 2, 132, 199)),
          IsHitTestVisible = false
      };

      System.Windows.Controls.Canvas.SetLeft(highlight, rect.X);
      System.Windows.Controls.Canvas.SetTop(highlight, rect.Y);
      HighlightCanvas.Children.Add(highlight);
  }
  ```

- [ ] **Step 5: 驗證建置與程式碼排版**
  本機執行格式檢查與建置：
  ```bash
  dotnet format AtlasForge.sln --verify-no-changes
  dotnet build AtlasForge.sln
  ```
  預期：無格式警告、建置成功且 0 警告/錯誤。

- [ ] **Step 6: Commit**
  ```bash
  git add src/AtlasForge/Views/Controls/AtlasPreviewControl.xaml.cs
  git commit -m "feat: implement preview frame selection highlight logic"
  ```

---

## Verification Plan

### Automated Tests
無自動化測試。本功能為 UI 互動與渲染展示，依據 `AGENTS.md` 規範：「UI 互動不測」。

### Manual Verification
1. 開氣程式並載入多張序列圖。
2. 切換選取左側「幀列表」中的任一幀，觀察右側預覽畫面是否正確於該圖片上標出主題藍色框線與藍色透明填滿。
3. 進行 Ctrl + 滑鼠滾輪縮放，確認標示框跟隨放大/縮小，且與下方圖片完全重合無偏差。
4. 按住滑鼠左鍵拖曳移動預覽圖，確認標示框也隨之移動。
5. 切換到「動畫」分頁，標示框應當立即隱藏。
6. 再切換回「Atlas」分頁，標示框應重現並對齊。
