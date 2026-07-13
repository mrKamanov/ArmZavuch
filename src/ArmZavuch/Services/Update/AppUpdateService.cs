using System.Net.Http;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using Velopack;
using Velopack.Sources;

namespace ArmZavuch.Services.Update;

/// <summary>
/// Проверка и установка обновлений: Velopack (установленная версия) + GitHub API (fallback).
/// </summary>
public sealed class AppUpdateService
{
    public const string LastUpdateCheckUtcKey = "last_update_check_utc";

    private readonly AppSettingsRepository _settings;
    private readonly GitHubReleaseClient _github;

    public AppUpdateService(AppSettingsRepository settings, GitHubReleaseClient github)
    {
        _settings = settings;
        _github = github;
    }

    public async Task<bool> ShouldCheckAutomaticallyAsync()
    {
        if (!UpdateChannelOptions.IsConfigured)
            return false;

        var lastRaw = await _settings.GetAsync(LastUpdateCheckUtcKey);
        if (string.IsNullOrWhiteSpace(lastRaw)
            || !DateTime.TryParse(lastRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last))
            return true;

        return DateTime.UtcNow - last.ToUniversalTime() >= UpdateChannelOptions.CheckInterval;
    }

    public async Task MarkCheckedAsync()
    {
        await _settings.SetAsync(LastUpdateCheckUtcKey, DateTime.UtcNow.ToString("O"));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!UpdateChannelOptions.IsConfigured)
            return new UpdateCheckResult.Skipped("Канал обновлений не настроен.");

        if (!await IsOnlineAsync(cancellationToken))
            return new UpdateCheckResult.Skipped("Нет подключения к интернету.");

        await MarkCheckedAsync();

        var velopackUpdate = await TryCheckVelopackAsync();
        if (velopackUpdate is not null)
            return new UpdateCheckResult.Available(velopackUpdate);

        var githubRelease = await _github.GetLatestAsync(cancellationToken);
        if (githubRelease is null)
            return new UpdateCheckResult.Failed("Не удалось получить информацию о релизе на GitHub.");

        if (!AppVersion.IsRemoteNewer(githubRelease.TagName))
            return new UpdateCheckResult.UpToDate($"Установлена актуальная версия {AppVersion.Current}.");

        return new UpdateCheckResult.Available(ToAvailableUpdate(githubRelease, canAutoInstall: false, velopackUpdate: null));
    }

    public async Task ApplyAsync(AvailableUpdate update, CancellationToken cancellationToken = default)
    {
        if (update.CanAutoInstall && update.VelopackUpdate is UpdateInfo info)
        {
            var mgr = CreateUpdateManager();
            await mgr.DownloadUpdatesAsync(info, progress: null, cancelToken: cancellationToken);
            mgr.ApplyUpdatesAndRestart(info.TargetFullRelease);
            return;
        }

        var url = update.SetupDownloadUrl ?? update.ReleasePageUrl;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }

    private async Task<AvailableUpdate?> TryCheckVelopackAsync()
    {
        try
        {
            var mgr = CreateUpdateManager();
            if (!mgr.IsInstalled)
                return null;

            var info = await mgr.CheckForUpdatesAsync();
            if (info is null)
                return null;

            return new AvailableUpdate
            {
                Version = info.TargetFullRelease.Version.ToString(),
                ReleaseNotes = info.TargetFullRelease.NotesMarkdown ?? "",
                ReleasePageUrl = UpdateChannelOptions.ReleasesPageUrl,
                SetupDownloadUrl = null,
                CanAutoInstall = true,
                VelopackUpdate = info
            };
        }
        catch
        {
            return null;
        }
    }

    private static UpdateManager CreateUpdateManager()
    {
        var source = new GithubSource(UpdateChannelOptions.RepositoryUrl, null, prerelease: false);
        return new UpdateManager(source);
    }

    private static AvailableUpdate ToAvailableUpdate(
        GitHubReleaseInfo release,
        bool canAutoInstall,
        UpdateInfo? velopackUpdate) =>
        new()
        {
            Version = release.TagName.TrimStart('v', 'V'),
            ReleaseNotes = release.ReleaseNotes,
            ReleasePageUrl = release.ReleasePageUrl,
            SetupDownloadUrl = release.SetupDownloadUrl,
            CanAutoInstall = canAutoInstall,
            VelopackUpdate = velopackUpdate
        };

    private static async Task<bool> IsOnlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync("https://api.github.com", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
