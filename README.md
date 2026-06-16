# AtlasForge

WPF 桌面工具，把一組序列圖（PNG frames）打包成 sprite atlas（精靈圖集），支援匯出 PNG + JSON（Unity）/ plist（Cocos）格式。

## 功能

- 兩種排列模式：Grid（固定行列）/ BinPack（自動緊密排列）
- Grid 模式可手動指定欄數，列數自動依幀數計算
- Alpha Trim（裁切透明邊界）、Padding（幀間留白）
- 最大 Atlas 尺寸限制（512 / 1024 / 2048 / 4096）
- 動畫預覽（逐幀播放剛打包好的 atlas）
- 匯出時選擇輸出資料夾

## 開發

```
dotnet build AtlasForge.sln
dotnet test AtlasForge.sln
dotnet format AtlasForge.sln --verify-no-changes
```

需求：.NET 8 SDK，Windows（WPF）。

## 專案結構

```
src/AtlasForge/
  Models/        ExportSettings、AtlasData 等資料模型
  Services/      封裝核心邏輯：BinPackingService、GridPackingService、
                 ImageProcessingService、ExportService、PreviewService
  ViewModels/    MainViewModel（MVVM，CommunityToolkit.Mvvm）
  Views/         WPF 視窗與控制項
tests/AtlasForge.Tests/   xUnit 單元測試
```

CI（`.github/workflows/build.yml`）每次 push/PR 自動跑 format 檢查、build、test。
