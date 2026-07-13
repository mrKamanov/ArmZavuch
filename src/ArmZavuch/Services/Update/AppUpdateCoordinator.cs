using ArmZavuch.Models;
using ArmZavuch.Services.Dialog;
using ArmZavuch.Services.Save;

namespace ArmZavuch.Services.Update;

/// <summary>
/// UI-слой обновлений: фоновая проверка раз в сутки и ручная из справки.
/// Решение об установке принимает пользователь.
/// </summary>
public sealed class AppUpdateCoordinator
{
    private readonly AppUpdateService _updates;
    private readonly IAppDialogService _dialogs;
    private readonly ISaveStateService _saveState;

    public AppUpdateCoordinator(
        AppUpdateService updates,
        IAppDialogService dialogs,
        ISaveStateService saveState)
    {
        _updates = updates;
        _dialogs = dialogs;
        _saveState = saveState;
    }

    public string VersionLabel => AppVersion.Display;

    public async Task CheckOnStartupIfDueAsync()
    {
        if (!await _updates.ShouldCheckAutomaticallyAsync())
            return;

        var result = await _updates.CheckAsync();
        if (result is UpdateCheckResult.Available available)
            PromptAndMaybeApply(available.Update, silentIfDeclined: true);
    }

    public async Task CheckManuallyAsync()
    {
        var result = await _updates.CheckAsync();
        switch (result)
        {
            case UpdateCheckResult.UpToDate ok:
                _dialogs.ShowInfo("Обновления", ok.Message);
                break;
            case UpdateCheckResult.Available available:
                PromptAndMaybeApply(available.Update, silentIfDeclined: false);
                break;
            case UpdateCheckResult.Failed failed:
                _dialogs.ShowWarning("Обновления", failed.Message);
                break;
            case UpdateCheckResult.Skipped skipped:
                _dialogs.ShowInfo("Обновления", skipped.Reason);
                break;
        }
    }

    private void PromptAndMaybeApply(AvailableUpdate update, bool silentIfDeclined)
    {
        var notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
            ? ""
            : $"\n\n{TrimReleaseNotes(update.ReleaseNotes, 600)}";

        var installHint = update.CanAutoInstall
            ? "Программа скачает обновление и перезапустится."
            : "Откроется страница загрузки установщика на GitHub.";

        var message =
            $"Доступна версия {update.Version}.\n" +
            $"Сейчас установлена {AppVersion.Current}.\n\n" +
            installHint +
            notes;

        var choice = _dialogs.AskUpdateAction(update.Version, message, update.CanAutoInstall);
        if (choice == UpdatePromptChoice.Later)
            return;

        if (_saveState.IsDirty
            && !_dialogs.ConfirmUpdateDespiteUnsaved(
                "Несохранённые изменения",
                "Перед обновлением лучше нажать Ctrl+S и сохранить данные."))
            return;

        if (choice == UpdatePromptChoice.InstallNow && update.CanAutoInstall)
        {
            _ = ApplyAsync(update);
            return;
        }

        _ = _updates.ApplyAsync(update);
    }

    private async Task ApplyAsync(AvailableUpdate update)
    {
        try
        {
            await _updates.ApplyAsync(update);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                "Обновление",
                $"Не удалось установить обновление.\n\n{ex.Message}\n\n" +
                $"Скачайте установщик вручную:\n{update.ReleasePageUrl}");
        }
    }

    private static string TrimReleaseNotes(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
}
