# AtlasForge

WPF 桌面工具，把一組序列圖（PNG frames）打包成 sprite atlas（精靈圖集），也能從既有 atlas 拆回序列圖。支援匯出 PNG + JSON（Unity）/ plist（Cocos）格式。

## 功能

- 兩種排列模式：Grid（固定行列）/ BinPack（自動緊密排列）
- Grid 模式可手動指定欄數，列數自動依幀數計算
- Alpha Trim（裁切透明邊界）、Padding（幀間留白）
- 最大 Atlas 尺寸限制（512 / 1024 / 2048 / 4096）
- Atlas 預覽選取高亮、縮放與拖曳平移
- 動畫預覽（逐幀播放剛打包好的 atlas）
- 序列拆圖：支援描述檔（JSON / plist）與網格切片
- 拆圖模式可載入合圖、預覽切片、輸出子圖 PNG
- 匯出時選擇輸出資料夾

## 開發

```
dotnet build AtlasForge.sln
dotnet test AtlasForge.sln
dotnet format AtlasForge.sln --verify-no-changes
```

需求：.NET 8 SDK，Windows（WPF）。

## 發布

目前版本：`0.1.2`

發布流程會在推送 `v*` tag 時由 GitHub Actions 執行：

1. `dotnet format AtlasForge.sln --verify-no-changes`
2. `dotnet build AtlasForge.sln --no-restore -c Release /warnaserror`
3. `dotnet test AtlasForge.sln --no-build -c Release`
4. `dotnet publish` 產出 win-x64 self-contained single-file
5. 建立 GitHub Release 並上傳 `AtlasForge-vX.Y.Z-win-x64.zip`

Release note 放在 `docs/release-notes/vX.Y.Z.md`。若該 tag 沒有對應檔案，workflow 會改用 GitHub 自動產生 notes。

## 專案結構

```
src/AtlasForge/
  Models/        ExportSettings、AtlasData 等資料模型
  Services/      封裝核心邏輯：BinPackingService、GridPackingService、
                 ImageProcessingService、ExportService、PreviewService、
                 UnpackService
  ViewModels/    MainViewModel（MVVM，CommunityToolkit.Mvvm）
  Views/         WPF 視窗與控制項
tests/AtlasForge.Tests/   xUnit 單元測試
```

CI（`.github/workflows/build.yml`）每次 push/PR 自動跑 format 檢查、build、test。
