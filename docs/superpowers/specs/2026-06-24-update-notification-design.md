# Update Notification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 在 AtlasForge 啟動時背景檢查 GitHub Releases，有新版時在 toolbar 右側顯示琥珀色 badge，點擊跳瀏覽器下載。

**Architecture:** `UpdateChecker` service 負責 GitHub API 呼叫與 24h cooldown 邏輯，結果透過 `MainViewModel` properties 綁定到 toolbar badge。失敗一律靜默，不影響 app 功能。

**Tech Stack:** .NET 8 / WPF，`System.Net.Http.HttpClient`，`System.Text.Json`，CommunityToolkit.Mvvm

---

## Section 1：架構 + 元件

| 動作 | 檔案 |
|------|------|
| Modify | `src/AtlasForge/AtlasForge.csproj` |
| Create | `src/AtlasForge/Services/UpdateChecker.cs` |
| Modify | `src/AtlasForge/ViewModels/MainViewModel.cs` |
| Modify | `src/AtlasForge/Views/MainWindow.xaml` |
| Create | `tests/AtlasForge.Tests/Services/UpdateCheckerTests.cs` |

## Section 2：Data Flow

```
MainViewModel 建構子
  → _ = CheckForUpdatesAsync()  (fire-and-forget)
       ↓
  UpdateChecker.CheckAsync()
       ├─ 讀 %AppData%\AtlasForge\update-check.json
       │     上次查詢 < 24h → 回傳快取結果
       │     超過 24h 或無快取 → GET GitHub API
       │         https://api.github.com/repos/Wenrong274/AtlasForge/releases/latest
       │         拿 tag_name + html_url
       │         寫回 update-check.json（含新時間戳 DateTime.UtcNow ISO 8601）
       └─ 比較 tag_name vs Assembly.GetName().Version
            有新版 → return UpdateInfo(LatestVersion, DownloadUrl)
            已最新 / 失敗 / offline → return null
       ↓
  MainViewModel：HasUpdate = true，設 LatestVersion / UpdateUrl
       ↓
  MainWindow.xaml badge Visibility 綁定 HasUpdate
```

**Cache 格式** (`%AppData%\AtlasForge\update-check.json`):

```json
{
  "checked_at": "2026-06-24T10:00:00Z",
  "latest_version": "v1.2.0",
  "download_url": "https://github.com/Wenrong274/AtlasForge/releases/tag/v1.2.0"
}
```

## Section 3：Error Handling

update check 失敗永遠靜默，絕不影響 app 啟動或使用。

| 情境 | 行為 |
|------|------|
| 無網路 / timeout 5秒 | 有快取 → 用快取；無快取 → badge 不顯示 |
| GitHub API 失敗 / rate limit | 靜默 catch，badge 不顯示 |
| cache 檔格式損壞 | 視為無快取，重新打 API |
| 版本號解析失敗 | badge 不顯示 |
| `%AppData%\AtlasForge\` 不存在 | 自動建立 |

所有 exception 在 `UpdateChecker.CheckAsync()` 內吃掉，回傳 `null`。

## UI 規格

- Toolbar 右對齊 badge button
- 背景色：`#F59E0B`（琥珀），Hover：`#D97706`
- 文字：`↑ {LatestVersion} 可用`（例：`↑ v1.2.0 可用`）
- 點擊：`Process.Start(new ProcessStartInfo(UpdateUrl) { UseShellExecute = true })`
- `Visibility`：`HasUpdate ? Visible : Collapsed`（用現有 `BoolToVisibilityConverter`）
