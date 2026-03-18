using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailScanner.App.Services;

public sealed class GitHubReleaseUpdateService
{
    private const string InstallerPrefix = "MailScanner-Setup-";
    private static readonly TimeSpan ReleaseCheckTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan InstallerDownloadTimeout = TimeSpan.FromMinutes(5);
    private readonly HttpClient releaseApiClient;
    private readonly HttpClient installerDownloadClient;
    private readonly string latestReleaseApiUrl;

    public GitHubReleaseUpdateService(string owner, string repository)
    {
        latestReleaseApiUrl = $"https://api.github.com/repos/{owner}/{repository}/releases/latest";
        releaseApiClient = CreateClient(ReleaseCheckTimeout);
        releaseApiClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        installerDownloadClient = CreateClient(InstallerDownloadTimeout);
    }

    public async Task<ReleaseUpdateInfo> GetLatestReleaseAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        using var response = await releaseApiClient.GetAsync(latestReleaseApiUrl, cancellationToken);
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
            release.Name ?? release.TagName ?? "GitHub Release",
            release.Body ?? string.Empty,
            GetPreferredInstallerAsset(release.Assets));
    }

    public async Task<string> DownloadInstallerAsync(ReleaseAssetInfo asset, string targetDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var filePath = Path.Combine(targetDirectory, asset.FileName);

        using var response = await installerDownloadClient.GetAsync(
            asset.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(filePath);

        if (totalBytes > 0 && progress != null)
        {
            var buffer = new byte[81920]; // 80KB buffer
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalBytesRead += bytesRead;
                double percent = (double)totalBytesRead / totalBytes * 100;
                progress.Report(Math.Min(percent, 100));
            }
        }
        else
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return filePath;
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

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient();
        client.Timeout = timeout;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MailScanner", "1.0"));
        return client;
    }

    private static ReleaseAssetInfo? GetPreferredInstallerAsset(IReadOnlyCollection<GitHubReleaseAssetDto>? assets)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        var installer = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name)
                && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
                && asset.Name.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase)
                && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(asset => asset.Name)
            .FirstOrDefault();

        return installer is null
            ? null
            : new ReleaseAssetInfo(installer.Name!, installer.BrowserDownloadUrl!);
    }

    public sealed record ReleaseAssetInfo(string FileName, string DownloadUrl);

    public sealed record ReleaseUpdateInfo(
        bool IsUpdateAvailable,
        string LatestVersion,
        string ReleaseUrl,
        string ReleaseTitle,
        string ReleaseNotes,
        ReleaseAssetInfo? InstallerAsset)
    {
        public static ReleaseUpdateInfo Unavailable() => new(false, string.Empty, string.Empty, string.Empty, string.Empty, null);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAssetDto[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}