# AtlasForge — CLAUDE.md / AGENTS.md

WPF 桌面工具（.NET 8 / Windows），把序列圖打包成 sprite atlas。

## 常用指令

```bash
dotnet build AtlasForge.sln
dotnet test AtlasForge.sln
dotnet format AtlasForge.sln --verify-no-changes   # CI 強制，commit 前必跑
dotnet run --project src/AtlasForge/AtlasForge.csproj
```

## 架構

**Pattern：MVVM + Services**

```
src/AtlasForge/
  Models/       純資料型別（AtlasData、ExportSettings、FrameData、SpriteRect）
  Services/     核心邏輯，不依賴 UI（BinPacking、GridPacking、ImageProcessing、Export、Preview）
  ViewModels/   MainViewModel（CommunityToolkit.Mvvm：ObservableObject、RelayCommand）
  Views/        WPF XAML + code-behind（MainWindow、Controls、Converters）
tests/AtlasForge.Tests/   xUnit，依 src 目錄結構鏡像
```

**依賴：**

- `CommunityToolkit.Mvvm` — `[ObservableProperty]`、`[RelayCommand]`
- `SkiaSharp` — 影像處理與 atlas 合成

## 程式碼規範

- C# nullable enable，implicit usings
- `dotnet format` 強制執行（CI 會擋），commit 前先跑
- Services 不依賴 ViewModel 或 View；ViewModel 不直接操作 UI
- 非 UI 執行緒的 UI 更新走 `RunOnUiThread()`（見 MainViewModel）
- UI 文字用**繁體中文**

## 測試

- xUnit，不用 Mock 框架
- 測試 Services 直接 new 實例（見 `BinPackingServiceTests`）
- UI 互動不測，ViewModel 邏輯視情況測

## CI / Release

- `build.yml`：每次 push/PR → format check、build、test
- `release.yml`：push `v*` tag → publish win-x64 single-file → GitHub Release
- 發版流程：`git tag v1.x.x && git push origin v1.x.x`

## 主題色（AppStyles.xaml）

| Key | Color |
|-----|-------|
| BgDark | `#0F172A` |
| BgMid | `#1E293B` |
| BgLight | `#334155` |
| Accent | `#0284C7` |
| TextPrimary | `#E2E8F0` |
| TextSecondary | `#94A3B8` |
| TextMuted | `#64748B` |

## 注意事項

- `csproj` 沒有 `<Version>` tag → 新增 feature 如需讀版本號，先加上去
- `%AppData%\AtlasForge\` 為 user 資料目錄（update cache 等放這裡）
- SkiaSharp bitmap 用完要 dispose（Services 內部處理，caller 不需擔心）
