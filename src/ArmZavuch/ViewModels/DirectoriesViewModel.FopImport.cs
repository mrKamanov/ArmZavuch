using ArmZavuch.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

public partial class DirectoriesViewModel
{
    [ObservableProperty] private SchoolClass? _fopImportClass;
    [ObservableProperty] private int _fopImportGrade = 1;

    public IAsyncRelayCommand ImportFopGapsForClassCommand { get; private set; } = null!;
    public IAsyncRelayCommand ImportFopOverwriteForClassCommand { get; private set; } = null!;
    public IAsyncRelayCommand ImportFopGapsForGradeCommand { get; private set; } = null!;
    public IAsyncRelayCommand ImportFopOverwriteForGradeCommand { get; private set; } = null!;

    partial void OnFopImportClassChanged(SchoolClass? value)
    {
        if (value is not null)
            FopImportGrade = value.Grade;
    }

    private void InitFopImportCommands()
    {
        ImportFopGapsForClassCommand = new AsyncRelayCommand(
            () => ImportFopForClassAsync(FopWorkloadImportOptions.FillGapsOnly));
        ImportFopOverwriteForClassCommand = new AsyncRelayCommand(
            () => ImportFopForClassAsync(FopWorkloadImportOptions.OverwriteHours));
        ImportFopGapsForGradeCommand = new AsyncRelayCommand(
            () => ImportFopForGradeAsync(FopWorkloadImportOptions.FillGapsOnly));
        ImportFopOverwriteForGradeCommand = new AsyncRelayCommand(
            () => ImportFopForGradeAsync(FopWorkloadImportOptions.OverwriteHours));
    }

    private async Task ImportFopForClassAsync(FopWorkloadImportOptions options)
    {
        if (FopImportClass is null)
        {
            _dialogs.ShowInfo("Часы по ФОП", "Выберите класс в списке.");
            return;
        }

        var preview = await _fopWorkload.PreviewForClassAsync(FopImportClass.Id);
        if (!ConfirmFopImport(FopImportClass.DisplayName, preview, options.OverwriteExistingHours))
            return;

        var result = await _fopWorkload.ImportForClassAsync(FopImportClass.Id, options);
        await ApplyFopImportResultAsync(result, FopImportClass.DisplayName);
    }

    private async Task ImportFopForGradeAsync(FopWorkloadImportOptions options)
    {
        var classes = ClassList.Where(c => c.Grade == FopImportGrade).ToList();
        if (classes.Count == 0)
        {
            _dialogs.ShowInfo("Часы по ФОП", $"В справочниках нет классов {FopImportGrade} параллели.");
            return;
        }

        var preview = new FopWorkloadImportPreview(0, 0, 0);
        foreach (var cls in classes)
        {
            var part = await _fopWorkload.PreviewForClassAsync(cls.Id);
            preview = new FopWorkloadImportPreview(
                preview.MissingSubjects + part.MissingSubjects,
                preview.ExistingWithDifferentHours + part.ExistingWithDifferentHours,
                preview.AlreadyMatching + part.AlreadyMatching);
        }

        var label = $"{classes.Count} класс(ов) {FopImportGrade} параллели";
        if (!ConfirmFopImport(label, preview, options.OverwriteExistingHours))
            return;

        var result = await _fopWorkload.ImportForGradeAsync(FopImportGrade, options);
        await ApplyFopImportResultAsync(result, label);
    }

    private bool ConfirmFopImport(string scopeLabel, FopWorkloadImportPreview preview, bool overwriteHours)
    {
        if (preview.MissingSubjects == 0
            && (!overwriteHours || preview.ExistingWithDifferentHours == 0))
        {
            _dialogs.ShowInfo(
                "Часы по ФОП",
                overwriteHours
                    ? $"Для {scopeLabel} все предметы ФОП уже есть с теми же часами."
                    : $"Для {scopeLabel} нечего добавлять — все предметы ФОП уже в нагрузке.");
            return false;
        }

        var lines = new List<string>
        {
            overwriteHours
                ? $"Перезаписать ч/нед по ФОП для {scopeLabel}?"
                : $"Добавить недостающие предметы ФОП для {scopeLabel}?"
        };

        if (preview.MissingSubjects > 0)
            lines.Add($"• добавится предметов: {preview.MissingSubjects}");
        if (overwriteHours && preview.ExistingWithDifferentHours > 0)
            lines.Add($"• обновится часов у строк: {preview.ExistingWithDifferentHours}");
        if (preview.AlreadyMatching > 0)
            lines.Add($"• без изменений: {preview.AlreadyMatching}");

        lines.Add("");
        lines.Add("Баллы Сивкова и назначенные педагоги не меняются.");

        return _dialogs.ConfirmProceed("Часы по ФОП", string.Join("\n", lines));
    }

    private async Task ApplyFopImportResultAsync(FopWorkloadImportResult result, string scopeLabel)
    {
        _saveState.MarkDirty();
        _revision.NotifyReferenceDataChanged();
        await ReloadAfterMutationAsync();

        StatusMessage =
            $"ФОП для {scopeLabel}: добавлено {result.Added}, обновлено часов {result.HoursUpdated}, без изменений {result.Skipped}.";
        _dialogs.ShowInfo(
            "Часы по ФОП",
            $"Для {scopeLabel}:\n" +
            $"• добавлено предметов: {result.Added}\n" +
            $"• обновлено часов: {result.HoursUpdated}\n" +
            $"• без изменений: {result.Skipped}\n\n" +
            "Баллы Сивкова не затронуты. Сохраните: Ctrl+S.");
    }
}
