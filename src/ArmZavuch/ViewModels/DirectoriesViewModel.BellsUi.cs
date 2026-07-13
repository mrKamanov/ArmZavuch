using System.Collections.ObjectModel;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Schedule;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArmZavuch.ViewModels;

/// <summary>UI шаблонов звонков: список шаблонов, хронология, назначения (ТЗ §3).</summary>
public partial class DirectoriesViewModel
{
    private List<BellTemplateRow> _bellTemplateRows = [];
    private bool _clearingBellSelection;

    public ObservableCollection<BellTemplateCard> BellTemplateCards { get; } = [];
    public ObservableCollection<BellPeriod> SelectedTemplateTimeline { get; } = [];

    [ObservableProperty] private BellTemplateCard? _selectedBellTemplateCard;
    [ObservableProperty] private int _selectedBellEditorShift = 1;
    [ObservableProperty] private string _editBellTemplateName = "";
    [ObservableProperty] private string _editBellGradeFrom = "1";
    [ObservableProperty] private string _editBellGradeTo = "11";

    public bool BellEditorHasShift1 =>
        SelectedBellTemplateCard?.HasShift1 != false;

    public bool BellEditorHasShift2 =>
        SelectedBellTemplateCard?.HasShift2 == true;

    public bool BellEditorShiftSelectorVisible => BellEditorHasShift1 && BellEditorHasShift2;

    public bool IsBellEditorShift1Selected
    {
        get => SelectedBellEditorShift == 1;
        set
        {
            if (value)
                SelectedBellEditorShift = 1;
        }
    }

    public bool IsBellEditorShift2Selected
    {
        get => SelectedBellEditorShift == 2;
        set
        {
            if (value)
                SelectedBellEditorShift = 2;
        }
    }

    public string BellEditorSummary
    {
        get
        {
            if (SelectedTemplateTimeline.Count == 0)
                return "Добавьте уроки и перемены — они пойдут в хронологическом порядке.";

            var ordered = BellScheduleTimelineSorter.OrderForDisplay(SelectedTemplateTimeline).ToList();
            var lessons = ordered.Count(p => BellPeriodKinds.IsLesson(p.PeriodKind));
            var first = ordered[0].StartTime;
            var last = ordered[^1].EndTime;
            var lessonDurations = ordered
                .Where(p => BellPeriodKinds.IsLesson(p.PeriodKind))
                .Select(p => BellTime.TryDurationMinutes(p.StartTime, p.EndTime))
                .Where(m => m.HasValue)
                .Select(m => m!.Value)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            var durationPart = lessonDurations.Count switch
            {
                0 => "",
                1 => $" · уроки по {lessonDurations[0]} мин",
                _ => $" · уроки {string.Join("/", lessonDurations)} мин"
            };

            return $"{lessons} урок(ов) · {first}–{last}{durationPart}";
        }
    }

    public string BellEditorHelpText =>
        "Выберите шаблон слева. Уроки, перемены и дин. паузы редактируются для выбранной смены. " +
        "После ввода уроков нажмите «Создать перемены» — промежутки заполнятся автоматически. " +
        "Время: 0830 превратится в 08:30; кнопки ± сдвигают на 5 минут. " +
        "Отдельный шаблон для класса — во вкладке «Классы». " +
        "Для II полугодия 1 класса выберите шаблон в назначении «1 класс · II полугодие» — с января он подставится автоматически.";

    partial void OnSelectedBellTemplateCardChanged(BellTemplateCard? value)
    {
        if (value is null)
        {
            EditBellTemplateName = "";
            EditBellGradeFrom = "1";
            EditBellGradeTo = "11";
        }
        else
        {
            EditBellTemplateName = value.Name;
            EditBellGradeFrom = value.GradeFrom.ToString();
            EditBellGradeTo = value.GradeTo.ToString();
            NewBellTemplate = value.Name;
            NewBellGradeFrom = value.GradeFrom.ToString();
            NewBellGradeTo = value.GradeTo.ToString();

            if (value.HasShift2 && !value.HasShift1)
                SelectedBellEditorShift = 2;
            else if (!value.HasShift2)
                SelectedBellEditorShift = 1;
        }

        FilterBellTemplate = value?.Name ?? "";
        SelectedBell = null;
        RefreshBellTimeline();
        ApplySuggestedBellSlot();
        OnPropertyChanged(nameof(BellEditorHasShift1));
        OnPropertyChanged(nameof(BellEditorHasShift2));
        OnPropertyChanged(nameof(BellEditorShiftSelectorVisible));
        OnPropertyChanged(nameof(IsBellEditorShift1Selected));
        OnPropertyChanged(nameof(IsBellEditorShift2Selected));
    }

    partial void OnSelectedBellEditorShiftChanged(int value)
    {
        NewBellShift = value.ToString();
        SelectedBell = null;
        RefreshBellTimeline();
        ApplySuggestedBellSlot();
        OnPropertyChanged(nameof(IsBellEditorShift1Selected));
        OnPropertyChanged(nameof(IsBellEditorShift2Selected));
    }

    [RelayCommand]
    private async Task CreateBellTemplateAsync()
    {
        var suggested = SuggestNewBellTemplateName();
        var name = _dialogs.PromptForText(
            "Новый шаблон звонков",
            "Название шаблона. Можно скопировать из выбранного — «Создать копию» быстрее.",
            suggested);
        if (name is null)
            return;

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (BellTemplateNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _dialogs.ShowWarning("Звонки", $"Шаблон «{name}» уже существует.");
            return;
        }

        var gradeFrom = int.TryParse(EditBellGradeFrom, out var gf) ? gf : 1;
        var gradeTo = int.TryParse(EditBellGradeTo, out var gt) ? gt : gradeFrom;
        if (gradeTo < gradeFrom)
            (gradeFrom, gradeTo) = (gradeTo, gradeFrom);

        try
        {
            await _bells.EnsureTemplateAsync(name, gradeFrom, gradeTo);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            SelectBellTemplateByName(name);
            _dialogs.ShowInfo("Звонки", $"Создан шаблон «{name}». Добавьте строки расписания справа.");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Звонки", $"Не удалось создать шаблон.\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DuplicateBellTemplateAsync()
    {
        if (SelectedBellTemplateCard is null)
        {
            _dialogs.ShowInfo("Звонки", "Сначала выберите шаблон для копирования.");
            return;
        }

        var source = SelectedBellTemplateCard;
        var suggested = $"{source.DisplayName} · копия";
        var name = _dialogs.PromptForText(
            "Копия шаблона",
            $"Будут скопированы все строки из «{source.DisplayName}».",
            suggested,
            confirmButtonText: "Создать копию");
        if (name is null)
            return;

        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (BellTemplateNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _dialogs.ShowWarning("Звонки", $"Шаблон «{name}» уже существует.");
            return;
        }

        try
        {
            await _bells.DuplicateTemplateAsync(source.TemplateId, name, source.GradeFrom, source.GradeTo);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            SelectBellTemplateByName(name);
            StatusMessage = $"Создана копия «{name}»";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Звонки", $"Не удалось скопировать шаблон.\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteBellTemplateAsync()
    {
        if (SelectedBellTemplateCard is null)
        {
            _dialogs.ShowInfo("Звонки", "Выберите шаблон для удаления.");
            return;
        }

        var card = SelectedBellTemplateCard;
        var classCount = await _bells.CountClassesUsingTemplateAsync(card.TemplateId);
        var isDefault = !string.IsNullOrWhiteSpace(card.DefaultUsageDisplay);
        if (isDefault || classCount > 0)
        {
            var parts = new List<string>();
            if (isDefault)
                parts.Add("шаблон указан в назначениях по умолчанию");
            if (classCount > 0)
                parts.Add($"используется в {classCount} класс(ах)");
            _dialogs.ShowWarning(
                "Звонки",
                $"Шаблон «{card.DisplayName}» нельзя удалить: {string.Join("; ", parts)}.");
            return;
        }

        if (!_dialogs.ConfirmProceed(
                "Удалить шаблон?",
                $"Будут удалены все строки шаблона «{card.DisplayName}»."))
            return;

        try
        {
            await _bells.DeleteTemplateAsync(card.TemplateId);
            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            StatusMessage = $"Шаблон «{card.DisplayName}» удалён";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Звонки", $"Не удалось удалить шаблон.\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveBellTemplateMetaAsync()
    {
        if (SelectedBellTemplateCard is null)
            return;

        var name = EditBellTemplateName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!int.TryParse(EditBellGradeFrom, out var gradeFrom)
            || !int.TryParse(EditBellGradeTo, out var gradeTo))
            return;

        if (gradeTo < gradeFrom)
            (gradeFrom, gradeTo) = (gradeTo, gradeFrom);

        var card = SelectedBellTemplateCard;
        var oldName = card.Name;
        if (!name.Equals(oldName, StringComparison.OrdinalIgnoreCase)
            && BellTemplateNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            _dialogs.ShowWarning("Звонки", $"Шаблон «{name}» уже существует.");
            return;
        }

        try
        {
            if (!name.Equals(oldName, StringComparison.Ordinal))
                await _bells.RenameTemplateAsync(card.TemplateId, name);

            await _bells.UpdateTemplateMetaAsync(card.TemplateId, name, gradeFrom, gradeTo);

            if (DefaultBellGrade1.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                DefaultBellGrade1 = name;
            if (DefaultBellGrade1SecondHalf.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                DefaultBellGrade1SecondHalf = name;
            if (DefaultBellShift1.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                DefaultBellShift1 = name;
            if (DefaultBellShift2.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                DefaultBellShift2 = name;

            if (DefaultBellGrade1 != _bellAssignment.DefaultGrade1
                || DefaultBellGrade1SecondHalf != _bellAssignment.DefaultGrade1SecondHalf
                || DefaultBellShift1 != _bellAssignment.DefaultShift1
                || DefaultBellShift2 != _bellAssignment.DefaultShift2)
                await _bellAssignment.SaveDefaultsAsync(
                    DefaultBellGrade1,
                    DefaultBellGrade1SecondHalf,
                    DefaultBellShift1,
                    DefaultBellShift2);

            foreach (var period in BellList.Where(p => p.TemplateId == card.TemplateId))
            {
                period.TemplateName = name;
                period.TemplateGradeFrom = gradeFrom;
                period.TemplateGradeTo = gradeTo;
            }

            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            SelectBellTemplateByName(name);
            StatusMessage = $"Шаблон «{BellTemplateNaming.ToDisplay(name)}» сохранён";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Звонки", $"Не удалось сохранить шаблон.\n{ex.Message}");
        }
    }

    private void RefreshBellTemplateCards()
    {
        var previousName = SelectedBellTemplateCard?.Name;
        BellTemplateCards.Clear();

        var rows = _bellTemplateRows.Count > 0
            ? _bellTemplateRows
            : BellList
                .GroupBy(p => p.TemplateId)
                .Select(g =>
                {
                    var first = g.First();
                    return new BellTemplateRow(first.TemplateId, first.TemplateName, first.TemplateGradeFrom, first.TemplateGradeTo);
                })
                .ToList();

        foreach (var row in rows.OrderBy(r => r.GradeFrom).ThenBy(r => r.Name))
        {
            var periods = BellList.Where(p => p.TemplateId == row.Id).ToList();
            var shift1 = periods.Count == 0 || periods.Any(p => p.Shift == 1);
            var shift2 = periods.Any(p => p.Shift == 2);
            var lessons = periods.Count(p => BellPeriodKinds.IsLesson(p.PeriodKind));
            var ordered = BellScheduleTimelineSorter.OrderForDisplay(periods).ToList();
            var dayRange = ordered.Count > 0
                ? BellTime.FormatRange(ordered[0].StartTime, ordered[^1].EndTime)
                : null;

            BellTemplateCards.Add(new BellTemplateCard
            {
                TemplateId = row.Id,
                Name = row.Name,
                GradeFrom = row.GradeFrom,
                GradeTo = row.GradeTo,
                LessonCount = lessons,
                PeriodCount = periods.Count,
                HasShift1 = shift1,
                HasShift2 = shift2,
                DayRangeDisplay = dayRange,
                DefaultUsageDisplay = BuildBellDefaultUsage(row.Name)
            });
        }

        var next = string.IsNullOrWhiteSpace(previousName)
            ? BellTemplateCards.FirstOrDefault()
            : BellTemplateCards.FirstOrDefault(c => c.Name.Equals(previousName, StringComparison.OrdinalIgnoreCase))
              ?? BellTemplateCards.FirstOrDefault();

        SelectedBellTemplateCard = next;
    }

    private string BuildBellDefaultUsage(string templateName)
    {
        var parts = new List<string>();
        if (DefaultBellGrade1.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            parts.Add("1 класс · I пол.");
        if (DefaultBellGrade1SecondHalf.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            parts.Add("1 класс · II пол.");
        if (DefaultBellShift1.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            parts.Add("1 смена");
        if (DefaultBellShift2.Equals(templateName, StringComparison.OrdinalIgnoreCase))
            parts.Add("2 смена");

        return parts.Count == 0 ? "" : $"По умолчанию: {string.Join(", ", parts)}";
    }

    private void RefreshBellTimeline()
    {
        SelectedTemplateTimeline.Clear();
        FilteredBellList.Clear();

        if (SelectedBellTemplateCard is null)
        {
            OnPropertyChanged(nameof(BellEditorSummary));
            OnPropertyChanged(nameof(SelectionHint));
            return;
        }

        var name = SelectedBellTemplateCard.Name;
        foreach (var period in BellScheduleTimelineSorter.OrderForDisplay(
                     BellList.Where(p =>
                         p.TemplateName.Equals(name, StringComparison.OrdinalIgnoreCase)
                         && p.Shift == SelectedBellEditorShift)))
        {
            SelectedTemplateTimeline.Add(period);
            FilteredBellList.Add(period);
        }

        OnPropertyChanged(nameof(BellEditorSummary));
        OnPropertyChanged(nameof(SelectionHint));
    }

    private void SelectBellTemplateByName(string name)
    {
        SelectedBellTemplateCard = BellTemplateCards.FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private string SuggestNewBellTemplateName()
    {
        for (var i = 1; i < 100; i++)
        {
            var candidate = $"Шаблон {i}";
            if (!BellTemplateNames.Any(n => n.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return $"Шаблон {DateTime.Now:HHmmss}";
    }

    private void ApplySuggestedBellSlot()
    {
        if (SelectedBell is not null)
            return;

        var gradeFrom = int.TryParse(EditBellGradeFrom, out var gf) ? gf : 1;
        var suggestion = BellScheduleEditorHints.SuggestNext(SelectedTemplateTimeline.ToList(), gradeFrom);
        NewBellPeriodKind = suggestion.PeriodKind;
        NewBellLessonNumber = suggestion.LessonNumber.ToString();
        NewBellStartTime = suggestion.StartTime;
        NewBellEndTime = suggestion.EndTime;
        NewBellShift = SelectedBellEditorShift.ToString();
        if (SelectedBellTemplateCard is not null)
            NewBellTemplate = SelectedBellTemplateCard.Name;

        NewBellLessonDurationMinutes = ResolveDefaultBellLessonDurationMinutes(gradeFrom);
        RecalculateBellEndFromDuration();
        OnPropertyChanged(nameof(ShowBellDurationSelector));
    }

    private void SyncBellLessonDurationFromForm()
    {
        if (!ShowBellDurationSelector)
            return;

        var duration = BellTime.TryDurationMinutes(NewBellStartTime, NewBellEndTime);
        if (duration is 35 or 40 or 45)
            NewBellLessonDurationMinutes = duration.Value;
    }

    private void RecalculateBellEndFromDuration()
    {
        if (!ShowBellDurationSelector)
            return;

        if (string.IsNullOrWhiteSpace(NewBellStartTime))
            return;

        NewBellEndTime = BellTime.AddMinutes(NewBellStartTime, NewBellLessonDurationMinutes);
    }

    private static int ResolveDefaultBellLessonDurationMinutes(int gradeFrom) =>
        gradeFrom <= 1 ? 35 : 40;

    [RelayCommand]
    private void ShiftBellStartTime(string? deltaText)
    {
        if (!int.TryParse(deltaText, out var delta))
            return;

        NewBellStartTime = BellTime.ShiftMinutes(NewBellStartTime, delta);
        RecalculateBellEndFromDuration();
    }

    [RelayCommand]
    private void ShiftBellEndTime(string? deltaText)
    {
        if (!int.TryParse(deltaText, out var delta))
            return;

        NewBellEndTime = BellTime.ShiftMinutes(NewBellEndTime, delta);
    }

    [RelayCommand]
    private async Task GenerateBellBreaksAsync()
    {
        if (SelectedBellTemplateCard is null)
        {
            _dialogs.ShowInfo("Звонки", "Сначала выберите шаблон слева.");
            return;
        }

        var timeline = SelectedTemplateTimeline.ToList();
        var proposed = BellBreakGenerator.GenerateMissingBreaks(timeline);
        if (proposed.Count == 0)
        {
            _dialogs.ShowInfo(
                "Звонки",
                "Нечего добавить: перемены уже есть или между уроками нет промежутка.");
            return;
        }

        if (!_dialogs.ConfirmProceed(
                "Создать перемены",
                $"Будет добавлено перемен: {proposed.Count}. " +
                "Интервалы возьмутся из времени между соседними уроками. " +
                "Существующие перемены и дин. паузы не изменятся."))
            return;

        var card = SelectedBellTemplateCard;
        var gradeFrom = int.TryParse(EditBellGradeFrom, out var gf) ? gf : card.GradeFrom;
        var gradeTo = int.TryParse(EditBellGradeTo, out var gt) ? gt : card.GradeTo;
        if (gradeTo < gradeFrom)
            (gradeFrom, gradeTo) = (gradeTo, gradeFrom);

        try
        {
            var templateId = await _bells.EnsureTemplateAsync(card.Name, gradeFrom, gradeTo);
            foreach (var breakPeriod in proposed)
            {
                await _bells.InsertPeriodAsync(new BellPeriod
                {
                    TemplateId = templateId,
                    TemplateName = card.Name,
                    TemplateGradeFrom = gradeFrom,
                    TemplateGradeTo = gradeTo,
                    LessonNumber = breakPeriod.AfterLessonNumber,
                    Shift = SelectedBellEditorShift,
                    StartTime = breakPeriod.StartTime,
                    EndTime = breakPeriod.EndTime,
                    PeriodKind = BellPeriodKinds.Break
                });
            }

            _saveState.MarkDirty();
            await ReloadAfterMutationAsync();
            SelectBellTemplateByName(card.Name);
            BeginNewBell();
            StatusMessage = $"Добавлено перемен: {proposed.Count}";
        }
        catch (Exception ex)
        {
            _dialogs.ShowError("Звонки", $"Не удалось создать перемены.\n{ex.Message}");
        }
    }

    private void SanitizeBellTemplateUi()
    {
        if (SelectedBell is not null)
            return;

        if (SelectedBellTemplateCard is null && BellTemplateCards.Count > 0)
            SelectedBellTemplateCard = BellTemplateCards[0];
        else if (SelectedBellTemplateCard is not null
                 && !BellTemplateCards.Any(c => c.Name.Equals(SelectedBellTemplateCard.Name, StringComparison.OrdinalIgnoreCase)))
            SelectedBellTemplateCard = BellTemplateCards.FirstOrDefault();

        if (!IsKnownBellTemplate(NewBellTemplate))
            NewBellTemplate = SelectedBellTemplateCard?.Name
                              ?? BellTemplateNames.FirstOrDefault()
                              ?? BellTemplateNaming.Standard;
    }
}
