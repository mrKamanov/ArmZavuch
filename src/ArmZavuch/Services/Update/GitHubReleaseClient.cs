using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ArmZavuch.Services.Update;

/// <summary>Чтение последнего релиза через GitHub API (fallback и dev-сборки).</summary>
public sealed class GitHubReleaseClient
{
    private static readonly HttpClient Http = CreateClient();

    public async Task<GitHubReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!UpdateChannelOptions.IsConfigured)
            return null;

        using var response = await Http.GetAsync(UpdateChannelOptions.LatestReleaseApiUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var dto = await response.Content.ReadFromJsonAsync<GitHubReleaseDto>(cancellationToken: cancellationToken);
        if (dto is null || string.IsNullOrWhiteSpace(dto.TagName))
            return null;

        var setupUrl = dto.Assets?
            .FirstOrDefault(a => a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                                 && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            ?.BrowserDownloadUrl;

        return new GitHubReleaseInfo(
            dto.TagName.Trim(),
            dto.Body?.Trim() ?? "",
            dto.HtmlUrl?.Trim() ?? UpdateChannelOptions.ReleasesPageUrl,
            setupUrl);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ArmZavuch-UpdateChecker/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAssetDto>? Assets { get; init; }
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}

public sealed record GitHubReleaseInfo(
    string TagName,
    string ReleaseNotes,
    string ReleasePageUrl,
    string? SetupDownloadUrl);
