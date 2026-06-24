# Update Notification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** App 啟動時背景查 GitHub Releases，有新版時在 toolbar 右側顯示琥珀色 badge，點擊跳瀏覽器下載。

**Architecture:** `UpdateChecker` service 封裝 GitHub API + 24h cache 邏輯，回傳 `UpdateInfo?`。`MainViewModel` 建構子 fire-and-forget 呼叫它，結果寫入 observable properties，toolbar badge 透過 WPF binding 自動顯示。所有錯誤在 `UpdateChecker` 內靜默吃掉。

**Tech Stack:** .NET 8 / WPF，`System.Net.Http.HttpClient`，`System.Text.Json`，CommunityToolkit.Mvvm，xUnit

---

## File Map

| 動作 | 檔案 | 職責 |
|------|------|------|
| Modify | `src/AtlasForge/AtlasForge.csproj` | 加 `<Version>1.0.0</Version>` |
| Create | `src/AtlasForge/Services/UpdateChecker.cs` | GitHub API + 24h cooldown + cache |
| Modify | `src/AtlasForge/ViewModels/MainViewModel.cs` | update properties + command + async check |
| Modify | `src/AtlasForge/Views/Styles/AppStyles.xaml` | UpdateBadgeStyle（琥珀色） |
| Modify | `src/AtlasForge/Views/MainWindow.xaml` | toolbar 換 DockPanel，加 badge button |
| Create | `tests/AtlasForge.Tests/Services/UpdateCheckerTests.cs` | 測 `IsNewer()` 純邏輯 |

---

## Task 1: Add `<Version>` to csproj

**Files:**

- Modify: `src/AtlasForge/AtlasForge.csproj`

- [ ] **Step 1: Add version tag**

Open `src/AtlasForge/AtlasForge.csproj`. In `<PropertyGroup>`, after `<RootNamespace>AtlasForge</RootNamespace>`, add:

```xml
<Version>1.0.0</Version>
```

Adjust the version number to match your current/next release tag (e.g., if you plan to release `v1.2.0`, set `1.2.0`).

- [ ] **Step 2: Verify build**

```bash
dotnet build AtlasForge.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AtlasForge/AtlasForge.csproj
git commit -m "chore: add Version to csproj"
```

---

## Task 2: UpdateChecker Service (TDD)

**Files:**

- Create: `tests/AtlasForge.Tests/Services/UpdateCheckerTests.cs`
- Create: `src/AtlasForge/Services/UpdateChecker.cs`

### Step 1 — Write failing tests

- [ ] **Step 1: Create test file**

Create `tests/AtlasForge.Tests/Services/UpdateCheckerTests.cs`:

```csharp
using AtlasForge.Services;

namespace AtlasForge.Tests.Services;

public class UpdateCheckerTests
{
    [Fact]
    public void IsNewer_RemoteHigher_ReturnsTrue()
    {
        Assert.True(UpdateChecker.IsNewer("v2.0.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_SameVersion_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("v1.0.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_RemoteLower_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("v0.9.0", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_InvalidTag_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("invalid", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_TagWithVPrefix_ParsedCorrectly()
    {
        Assert.True(UpdateChecker.IsNewer("v1.2.3", new Version(1, 0, 0)));
    }

    [Fact]
    public void IsNewer_EmptyTag_ReturnsFalse()
    {
        Assert.False(UpdateChecker.IsNewer("", new Version(1, 0, 0)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test AtlasForge.sln --filter "UpdateCheckerTests"
```

Expected: FAIL — `AtlasForge.Services.UpdateChecker` does not exist.

### Step 3 — Implement UpdateChecker

- [ ] **Step 3: Create `UpdateChecker.cs`**

Create `src/AtlasForge/Services/UpdateChecker.cs`:

```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtlasForge.Services;

public record UpdateInfo(string LatestVersion, string DownloadUrl);

public class UpdateChecker
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly string _cacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasForge");
    private static readonly string _cachePath = Path.Combine(_cacheDir, "update-check.json");
    private const string ApiUrl = "https://api.github.com/repos/Wenrong274/AtlasForge/releases/latest";

    static UpdateChecker()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AtlasForge");
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var cached = TryLoadCache();
            if (cached != null && DateTime.UtcNow - cached.CheckedAt < TimeSpan.FromHours(24))
                return IsNewer(cached.LatestVersion) ? new UpdateInfo(cached.LatestVersion, cached.DownloadUrl) : null;

            var release = await FetchLatestAsync();
            if (release == null)
                return null;

            SaveCache(new CacheData(DateTime.UtcNow, release.TagName, release.HtmlUrl));
            return IsNewer(release.TagName) ? new UpdateInfo(release.TagName, release.HtmlUrl) : null;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(string tagName, Version? currentVersion = null)
    {
        currentVersion ??= Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        return Version.TryParse(tagName.TrimStart('v'), out var remote) && remote > currentVersion;
    }

    private static CacheData? TryLoadCache()
    {
        if (!File.Exists(_cachePath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CacheData>(File.ReadAllText(_cachePath));
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(CacheData data)
    {
        Directory.CreateDirectory(_cacheDir);
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(data));
    }

    private static async Task<ReleaseResponse?> FetchLatestAsync()
    {
        var resp = await _http.GetAsync(ApiUrl);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ReleaseResponse>();
    }
}

internal record CacheData(
    [property: JsonPropertyName("checked_at")] DateTime CheckedAt,
    [property: JsonPropertyName("latest_version")] string LatestVersion,
    [property: JsonPropertyName("download_url")] string DownloadUrl);

internal record ReleaseResponse(
    [property: JsonPropertyName("tag_name")] string TagName,
    [property: JsonPropertyName("html_url")] string HtmlUrl);
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test AtlasForge.sln --filter "UpdateCheckerTests"
```

Expected: 6 tests passed.

- [ ] **Step 5: Format check**

```bash
dotnet format AtlasForge.sln --verify-no-changes
```

Expected: No changes needed. If format errors appear, run `dotnet format AtlasForge.sln` then re-check.

- [ ] **Step 6: Commit**

```bash
git add src/AtlasForge/Services/UpdateChecker.cs tests/AtlasForge.Tests/Services/UpdateCheckerTests.cs
git commit -m "feat: add UpdateChecker service with 24h cooldown"
```

---

## Task 3: Wire UpdateChecker into MainViewModel

**Files:**

- Modify: `src/AtlasForge/ViewModels/MainViewModel.cs`

The existing `MainViewModel` is a `partial class` using `[ObservableProperty]` and `[RelayCommand]`. Add update state and an explicit constructor.

- [ ] **Step 1: Add observable properties and constructor**

Open `src/AtlasForge/ViewModels/MainViewModel.cs`. Add these lines after the existing `[ObservableProperty]` block (after `private string _outputPath = "";`):

```csharp
[ObservableProperty]
private bool _hasUpdate;

[ObservableProperty]
private string _latestVersion = string.Empty;

[ObservableProperty]
private string _updateUrl = string.Empty;
```

Then add a computed property and partial method after `_suppressGridPack`:

```csharp
public string UpdateBadgeText => $"↑ {LatestVersion} 可用";

partial void OnLatestVersionChanged(string value) => OnPropertyChanged(nameof(UpdateBadgeText));
```

- [ ] **Step 2: Add OpenUpdateCommand**

Add after the existing `[RelayCommand]` methods:

```csharp
[RelayCommand]
private void OpenUpdate()
{
    if (string.IsNullOrEmpty(UpdateUrl))
        return;

    System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
}
```

- [ ] **Step 3: Add constructor with update check**

Add after the `OpenUpdate` method:

```csharp
public MainViewModel()
{
    _ = CheckForUpdatesAsync();
}

private async Task CheckForUpdatesAsync()
{
    var info = await new UpdateChecker().CheckAsync();
    if (info is null)
        return;

    RunOnUiThread(() =>
    {
        LatestVersion = info.LatestVersion;
        UpdateUrl = info.DownloadUrl;
        HasUpdate = true;
    });
}
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test AtlasForge.sln
```

Expected: All existing tests still pass, 6 new `UpdateCheckerTests` pass.

- [ ] **Step 5: Format check**

```bash
dotnet format AtlasForge.sln --verify-no-changes
```

- [ ] **Step 6: Commit**

```bash
git add src/AtlasForge/ViewModels/MainViewModel.cs
git commit -m "feat: wire UpdateChecker into MainViewModel"
```

---

## Task 4: Add UpdateBadgeStyle and Toolbar Badge

**Files:**

- Modify: `src/AtlasForge/Views/Styles/AppStyles.xaml`
- Modify: `src/AtlasForge/Views/MainWindow.xaml`

### Step 1 — Add style to AppStyles.xaml

- [ ] **Step 1: Add `UpdateBadgeStyle`**

Open `src/AtlasForge/Views/Styles/AppStyles.xaml`. Before the closing `</ResourceDictionary>` tag, add:

```xml
<Style x:Key="UpdateBadgeStyle" TargetType="Button">
    <Setter Property="Background" Value="#F59E0B" />
    <Setter Property="Foreground" Value="#1C1917" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Padding" Value="8,3" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}"
                        CornerRadius="4"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter Property="Background" Value="#D97706" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### Step 2 — Replace toolbar StackPanel with DockPanel

The current toolbar uses `<StackPanel Orientation="Horizontal">` which can't right-align elements. Replace it with `<DockPanel LastChildFill="False">`.

- [ ] **Step 2: Replace toolbar layout in `MainWindow.xaml`**

Open `src/AtlasForge/Views/MainWindow.xaml`. Find the `<StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="12,0">` block inside the top `<Border>` and replace the entire StackPanel with:

```xml
<DockPanel VerticalAlignment="Center"
           Margin="12,0"
           LastChildFill="False">
    <Button DockPanel.Dock="Right"
            Style="{StaticResource UpdateBadgeStyle}"
            Content="{Binding UpdateBadgeText}"
            Command="{Binding OpenUpdateCommand}"
            Visibility="{Binding HasUpdate, Converter={StaticResource BoolToVis}}" />
    <TextBlock DockPanel.Dock="Left"
               Text="⚡ AtlasForge"
               Foreground="#38BDF8"
               FontWeight="Bold"
               FontSize="13"
               VerticalAlignment="Center" />
    <Separator DockPanel.Dock="Left"
               Width="1"
               Background="{StaticResource BgLight}"
               Margin="12,6" />
    <Button DockPanel.Dock="Left"
            Content="📂 開啟資料夾"
            Style="{StaticResource ToolbarButtonStyle}"
            Margin="0,0,4,0"
            Click="OpenFolder_Click" />
    <Button DockPanel.Dock="Left"
            Content="📄 選擇檔案"
            Style="{StaticResource ToolbarButtonStyle}"
            Margin="0,0,4,0"
            Click="OpenFiles_Click" />
    <Button DockPanel.Dock="Left"
            Content="🗑 清除"
            Style="{StaticResource ToolbarButtonStyle}"
            Command="{Binding ClearFramesCommand}" />
</DockPanel>
```

Note: The update badge button is declared **first** with `DockPanel.Dock="Right"` so it anchors to the right edge. Left-side buttons follow.

- [ ] **Step 3: Build and verify**

```bash
dotnet build AtlasForge.sln
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Format check**

```bash
dotnet format AtlasForge.sln --verify-no-changes
```

- [ ] **Step 5: Run tests**

```bash
dotnet test AtlasForge.sln
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/AtlasForge/Views/Styles/AppStyles.xaml src/AtlasForge/Views/MainWindow.xaml
git commit -m "feat: add update notification badge to toolbar"
```

---

## Manual Smoke Test

After all tasks complete, verify end-to-end:

- [ ] Run app: `dotnet run --project src/AtlasForge/AtlasForge.csproj`
- [ ] Badge hidden on first launch (version matches or no network)
- [ ] Simulate new version: temporarily set `<Version>0.0.1</Version>` in csproj and delete `%AppData%\AtlasForge\update-check.json`
- [ ] Rerun → badge `↑ vX.X.X 可用` appears in toolbar right side
- [ ] Click badge → GitHub releases page opens in browser
- [ ] Rerun without deleting cache → no second API call within 24h (check `update-check.json` timestamp)
- [ ] Restore original version in csproj
