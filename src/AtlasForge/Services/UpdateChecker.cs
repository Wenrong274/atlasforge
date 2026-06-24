using System.IO;
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
        var cached = TryLoadCache();

        if (cached != null && DateTime.UtcNow - cached.CheckedAt < TimeSpan.FromHours(24))
            return IsNewer(cached.LatestVersion) ? new UpdateInfo(cached.LatestVersion, cached.DownloadUrl) : null;

        try
        {
            var release = await FetchLatestAsync();
            if (release == null)
                return null;

            SaveCache(new CacheData(DateTime.UtcNow, release.TagName, release.HtmlUrl));
            return IsNewer(release.TagName) ? new UpdateInfo(release.TagName, release.HtmlUrl) : null;
        }
        catch
        {
            // Network failed — fall back to stale cache if available
            return cached != null && IsNewer(cached.LatestVersion)
                ? new UpdateInfo(cached.LatestVersion, cached.DownloadUrl)
                : null;
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
        using var resp = await _http.GetAsync(ApiUrl);
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