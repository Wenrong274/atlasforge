# 預覽畫面選取幀高亮標示設計文件 (Frame Selection Highlight Design)

此設計文件說明如何在點選左側序列圖（幀列表）時，於右側 Atlas 預覽畫面中高亮標示對應的圖片位置。

## 需求概述

當使用者在左側的「幀列表」點選任一幀時，右側的「Atlas 預覽畫面」能夠立即在該幀所屬的區塊上顯示一個外框或變色標示（本設計採用 **主題藍色框線 + 15% 透明度填滿**），以便使用者快速辨識該幀在 Atlas 上的具體位置。

## 解決方案：WPF 覆蓋畫布 (Overlay Canvas)

本功能採用 WPF 原生的覆蓋畫布方案，避免重新繪製 Bitmap 帶來的效能損耗。

### 1. XAML 結構調整
將 `AtlasPreviewControl.xaml` 中的 `PreviewImage` 用一個 `Grid` 容器（`ImageContainer`）包裝起來，並加入一個 `<Canvas x:Name="HighlightCanvas" />` 作為重疊層。

```xml
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

由於縮放功能會改變圖片大小，我們需要將 `PreviewImage.LayoutTransform` 改為作用於 `ImageContainer.LayoutTransform`，如此一來 `ImageContainer` 底下的 `Image` 與 `HighlightCanvas` 將會同步縮放，保持座標的 1:1 對齊。

### 2. Code-Behind 控制邏輯 (`AtlasPreviewControl.xaml.cs`)
- **監聽 DataContext (MainViewModel) 屬性變更**：
  在 `DataContextChanged` 事件中，訂閱或取消訂閱 `MainViewModel.PropertyChanged`。當 `SelectedFrame`、`CurrentAtlas` 或 `AtlasPreview` 屬性變更時，觸發更新。
- **切換 Tab 更新**：
  在 `AtlasTab_Click` 與 `AnimTab_Click` 中呼製 `UpdateHighlight()`。只有在 **Atlas 預覽模式** 下才顯示高亮外框。
- **高亮繪製邏輯 (`UpdateHighlight`)**：
  1. 清空 `HighlightCanvas.Children`。
  2. 若目前不是 Atlas 模式、VM 為空、或未選取幀，則直接返回。
  3. 獲取選取幀的 `OrderIndex`，並從 `CurrentAtlas.Frames[OrderIndex]` 取得對應的 `SpriteRect` 位置（`X`, `Y`, `Width`, `Height`）。
  4. 實例化一個 WPF `Border` 元件，設定其大小、邊框（使用主題 `Accent` 筆刷，粗細為 2）、背景顏色（藍色主題色 15% 透明度 `#280284C7`），並透過 `Canvas.SetLeft` 與 `Canvas.SetTop` 定位至 `HighlightCanvas` 上。

---

## 驗證計畫

### 手動測試
1. 載入一組序列圖。
2. 點選左側「幀列表」中的第 3 張圖。
3. 驗證右側「Atlas」預覽畫面上對應的第 3 張圖是否出現藍色透明高亮外框。
4. 進行縮放（Ctrl + 滾輪 或 點選 ＋/－ 按鈕），驗證高亮外框是否與圖片完美同步縮放且位置無位移。
5. 進行拖曳移動，驗證高亮外框是否跟隨移動。
6. 切換至「動畫」分頁，驗證高亮外框是否消失。
7. 切換回「Atlas」分頁，驗證高亮外框是否重新正確顯示。
8. 點選「清除」按鈕，驗證高亮外框是否隨之消失。
