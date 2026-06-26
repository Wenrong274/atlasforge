# AtlasForge — CLAUDE.md / AGENTS.md

WPF 桌面工具（.NET 8 / Windows），把序列圖打包成 sprite atlas，並支援從既有 atlas 拆回序列圖。

## 常用指令

```bash
dotnet build AtlasForge.sln
dotnet test AtlasForge.sln
dotnet format AtlasForge.sln --verify-no-changes   # CI 強制，commit 前必跑
dotnet run --project src/AtlasForge/AtlasForge.csproj

# 發布前同 release workflow 驗證
dotnet build AtlasForge.sln --no-restore -c Release /warnaserror
dotnet test AtlasForge.sln --no-build -c Release
dotnet publish src/AtlasForge/AtlasForge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## 架構

**Pattern：MVVM + Services**

```
src/AtlasForge/
  Models/       純資料型別（AtlasData、ExportSettings、FrameData、SpriteRect）
  Services/     核心邏輯，不依賴 UI（BinPacking、GridPacking、ImageProcessing、Export、Preview、Unpack）
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
- 打包/拆圖核心邏輯放 Services；WPF 控制項只處理互動、檔案選擇、預覽狀態
- SkiaSharp bitmap 生命週期要明確 dispose；傳給 WPF 的 `BitmapSource` 應 `Freeze()`

## 測試

- xUnit，不用 Mock 框架
- 測試 Services 直接 new 實例（見 `BinPackingServiceTests`）
- UI 互動不測，ViewModel 邏輯視情況測
- 不依賴本機絕對路徑或人工素材；需要圖片時在測試中產生臨時 PNG
- 拆圖 roundtrip 測試需涵蓋 plist/json 與輸出尺寸

## CI / Release

- `build.yml`：每次 push/PR → format check、build、test
- `release.yml`：push `v*` tag → format、Release build（warnings as errors）、test、publish win-x64 single-file → GitHub Release
- Release note 放 `docs/release-notes/vX.Y.Z.md`；若 tag 找不到對應 note，workflow 會退回 `--generate-notes`
- 發版流程：
  1. 更新 `src/AtlasForge/AtlasForge.csproj` 的 `<Version>`，需與 tag 去掉 `v` 後一致
  2. 新增/更新 `docs/release-notes/vX.Y.Z.md`
  3. 跑 format、Release build/test、必要時本機 publish
  4. commit 後 `git tag vX.Y.Z`
  5. `git push origin master vX.Y.Z`

## 主題色（AppStyles.xaml）

| Key | Color |
|-----|-------|
| BgDark | `#0F172A` |
| BgMid | `#1E293B` |
| BgLight | `#334155` |
| Accent | `#0284C7` |
| Accent（拆圖模式） | `#8B5CF6` |
| TextPrimary | `#E2E8F0` |
| TextSecondary | `#94A3B8` |
| TextMuted | `#64748B` |

## 注意事項

- `csproj` 已有 `<Version>` tag；匯出 JSON 的 `meta.version` 會讀取 assembly informational version
- `%AppData%\AtlasForge\` 為 user 資料目錄（update cache 等放這裡）
- SkiaSharp bitmap 用完要 dispose（Services 內部處理，caller 不需擔心）
- `docs/releases/` 會被 `.gitignore` 忽略；release note 請放 `docs/release-notes/`
