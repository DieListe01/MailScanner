using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailScanner.App.Services;

public sealed class GitHubReleaseUpdateService
{
    private readonly HttpClient httpClient;
    private readonly string latestReleaseApiUrl;

    public GitHubReleaseUpdateService(string owner, string repository)
    {
        latestReleaseApiUrl = $"https://api.github.com/repos/{owner}/{repository}/releases/latest";
        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MailScanner", "1.0"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<ReleaseUpdateInfo> GetLatestReleaseAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(latestReleaseApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, cancellationToken: cancellationToken);

        if (release is null || release.Draft || release.PreRelease)
        {
            return ReleaseUpdateInfo.Unavailable();
        }

        var latestVersion = NormalizeVersion(release.TagName);
        var installedVersion = NormalizeVersion(currentVersion);
        var isUpdateAvailable = latestVersion is not null
            && installedVersion is not null
            && latestVersion > installedVersion;

        return new ReleaseUpdateInfo(
            isUpdateAvailable,
            release.TagName ?? "unbekannt",
            release.HtmlUrl ?? string.Empty,
            release.Name ?? release.TagName ?? "GitHub Release");
    }

    private static Version? NormalizeVersion(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var normalized = versionText.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out var version) ? version : null;
    }

    public sealed record ReleaseUpdateInfo(bool IsUpdateAvailable, string LatestVersion, string ReleaseUrl, string ReleaseTitle)
    {
        public static ReleaseUpdateInfo Unavailable() => new(false, string.Empty, string.Empty, string.Empty);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }
    }
}
