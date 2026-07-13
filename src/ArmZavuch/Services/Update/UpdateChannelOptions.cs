namespace ArmZavuch.Services.Update;

/// <summary>Канал обновлений через GitHub Releases (Velopack + API).</summary>
public static class UpdateChannelOptions
{
    /// <summary>Владелец репозитория GitHub (организация или пользователь).</summary>
    public const string GitHubOwner = "mrKamanov";

    /// <summary>Имя репозитория GitHub с релизами.</summary>
    public const string GitHubRepo = "ArmZavuch";

    public static string RepositoryUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";

    public static string ReleasesPageUrl => $"{RepositoryUrl}/releases";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(GitHubOwner)
        && !GitHubOwner.Equals("REPLACE_ME", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(GitHubRepo);
}
