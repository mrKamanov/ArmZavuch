using ArmZavuch.Models;
using ArmZavuch.Services.Recovery;

namespace ArmZavuch.Services.Dialog;

public interface IAppDialogService
{
    AppDialogResult Show(AppDialogOptions options);
    bool ConfirmDelete(string label, DeleteEntityKind kind = DeleteEntityKind.Generic);
    bool ConfirmDeleteMany(int count, DeleteEntityKind kind = DeleteEntityKind.Generic);
    bool ConfirmClearAllData();
    bool ConfirmClearDirectorySection(DirectoryClearSection section);
    CurriculumClearMode? AskCurriculumClearMode();
    bool ConfirmImportAppData(AppDataTransferManifest manifest);
    bool ConfirmSelectiveImportAppData(
        AppDataTransferManifest manifest,
        IReadOnlyList<AppDataTransferSection> sections,
        AppDataImportMode mode);
    RecoveryChoice AskRecoveryChoice(DateTime draftTime, DateTime? savedTime);
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void ShowSuccess(string title, string message);
    /// <summary>Мягкое подтверждение: «Всё равно сохранить».</summary>
    bool ConfirmProceed(string title, string message);
    /// <summary>Диалог обновления: установить, открыть страницу или отложить.</summary>
    UpdatePromptChoice AskUpdateAction(string version, string message, bool canAutoInstall);
    /// <summary>Подтверждение обновления при несохранённых данных.</summary>
    bool ConfirmUpdateDespiteUnsaved(string title, string message);
    /// <summary>Диалог с полем ввода; null — отмена или пустая строка.</summary>
    string? PromptForText(
        string title,
        string message,
        string defaultText,
        string inputLabel = "Название",
        string confirmButtonText = "Создать");
}
