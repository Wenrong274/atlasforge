# 序列圖拆分/解包工具設計文件 (Texture Unpacker Design)

此設計文件說明如何在主畫面（MainWindow）中整合「拆圖工具 (Texture Unpacker)」作為一個獨立的分頁（UserControl），支援切換「合圖打包」與「序列拆圖」模式。

為提升視覺引導，本功能採用 **「動態主題切換」**：切換至拆圖模式時，主視窗的 Accent 主題色會由科技藍動態切換為 **「科技紫 (Violet/Purple)」**，讓使用者有明確的功能邊界感，而無須彈出額外視窗。

## 需求概述

1. **整合式分頁**：在 `MainWindow` 頂部工具列新增分頁切換按鈕：
   - **⚡ 合圖打包** (Packer Mode)
   - **🔧 序列拆圖** (Unpacker Mode)
2. **動態主題切換**：
   - 使用者切換模式時，主視窗動態變更 WPF Resources 的 `Accent` 與 `BorderColor` 等 Brush 值。
   - 打包模式：`Accent` = `#0284C7` (科技藍)，`BorderColor` = `#1E3A5F`
   - 拆圖模式：`Accent` = `#8B5CF6` (科技紫)，`BorderColor` = `#3A2E5C` (暗紫)
3. **模組化 UserControl (`UnpackerControl.xaml`)**：
   - 拆圖介面將封裝在獨立的控制項中，其三欄式佈局與邏輯（參數設定、切片清單、大圖預覽與高亮）與先前一致，但以 `UserControl` 形式嵌入，防止代碼污染 `MainWindow`。
4. **清理舊視窗**：移除舊的 `UnpackWindow.xaml` 與 `UnpackWindow.xaml.cs`。

---

## 介面與互動設計

### 1. `MainWindow.xaml` 結構調整
- 工具列最左側（`⚡ AtlasForge` 旁）放置分頁按鈕：
  - `<Button Content="⚡ 合圖打包" Style="{DynamicResource TabStyle}" ... />`
  - `<Button Content="🔧 序列拆圖" Style="{DynamicResource TabStyle}" ... />`
- 工具列右側的按鈕會根據當前模式動態顯示/隱藏（打包的開啟資料夾、檔案、清除等，在拆圖模式下隱藏）。
- 下方主內容區：
  - Packer Grid（原本的 FrameList、Preview、Settings）在 `Packer` 模式下顯示。
  - `ctrl:UnpackerControl` 在 `Unpacker` 模式下顯示。

### 2. `UnpackerControl.xaml` 設計
採用與原先相同的內建深色背景三欄佈局：
- **左側設定**：輸入合圖、設定切分模式（描述檔或網格）、輸出路徑、預覽與拆分按鈕。
- **中間清單**：使用 `ListBox` 呈現切片子圖縮圖與尺寸資訊（選取背景為紫色主題色）。
- **右側預覽**：支援 Canvas 高亮框（紫框 `#8B5CF6` 與 15% 透明紫填滿）。

---

## 核心服務與資料結構設計

### 1. 動態資源化 (`AppStyles.xaml` 與 `MainWindow`)
- [`AppStyles.xaml`](file:///d:/Dev/AtlasForge/src/AtlasForge/Views/Styles/AppStyles.xaml) 中所有與主題色相關的 `StaticResource Accent` 改為 `DynamicResource Accent`。
- `MainWindow` 的 Code-Behind 監聽分頁切換事件，在模式變更時，修改視窗的 `Resources`：
  ```csharp
  this.Resources["Accent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6"));
  this.Resources["AccentHover"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C3AED"));
  this.Resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A2E5C"));
  ```

### 2. 模式狀態管理
在 `MainWindow.xaml.cs` 中維護一個私有欄位（或 VM 屬性）來切換 UI：
- `private bool _isUnpackerMode;`
- 切換時，更新 Packer Grid 與 UnpackerControl 的 `Visibility`。
- 當處於 Unpacker 模式時，主視窗的標題可動態加上 `(拆圖模式)` 以作區別。

---

## 驗證計畫

### 手動測試流程
1. **模式切換與主題色變更**：
   - 點擊「🔧 序列拆圖」頁籤，主視窗的 Accent 按鈕、作用中頁籤邊框應立即變為科技紫，且中央區塊切換為拆圖介面。
   - 點擊「⚡ 合圖打包」頁籤，介面應切回原本的打包介面，且顏色復原為科技藍。
2. **拆圖預覽與切分測試**：
   - 在拆圖分頁載入合圖，設定網格 `4` 行 `4` 列。
   - 點擊「預覽分析」，中間清單應出現 16 個縮圖項目。
   - 點選清單項目，右側大圖對應區塊應亮起紫色高亮框。
   - 點擊「開始拆分」，確認輸出資料夾產生正確的子圖。

