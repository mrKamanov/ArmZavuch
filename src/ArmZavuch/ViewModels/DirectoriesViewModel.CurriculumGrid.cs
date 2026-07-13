using System.Collections.ObjectModel;
using System.ComponentModel;
using ArmZavuch.Models;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.ViewModels;

/// <summary>Назначение педагогов и правка полей строк нагрузки в таблице справочника.</summary>
public sealed partial class DirectoriesViewModel
{
    public ObservableCollection<Teacher> TeacherPickList { get; } = [];

    private Dictionary<int, List<int>> _curriculumAssigneeMap = [];
    private readonly List<Task> _pendingCurriculumGridSaves = [];

    private void RefreshTeacherPickList()
    {
        TeacherPickList.Clear();
        foreach (var teacher in TeacherList.OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase))
            TeacherPickList.Add(teacher);
    }

    /// <summary>Дожидается только уже запущенных сохранений; не перезаписывает таблицу из UI.</summary>
    public async Task WaitPendingCurriculumGridSavesAsync()
    {
        Task[] pending;
        lock (_pendingCurriculumGridSaves)
            pending = _pendingCurriculumGridSaves.ToArray();
        if (pending.Length > 0)
            await Task.WhenAll(pending);
    }

    private async Task RefreshTeachersForStaffTabAsync()
    {
        await WaitPendingCurriculumGridSavesAsync();
        foreach (var teacher in TeacherList)
            await _teachers.RefreshCurriculumAssignmentsAsync(teacher);

        if (SelectedTeacher is not null)
            await LoadTeacherDetailsAsync();
    }

    private async Task ReloadCurriculumAssigneeMapFromDbAsync()
    {
        _curriculumAssigneeMap = await _teachers.GetExplicitAssigneesByCurriculumAsync();
    }

    private void RefreshCurriculumGroupsFromAssigneeMap()
    {
        foreach (var group in CurriculumGroups)
        {
            foreach (var row in group.Rows)
            {
                var (primary, secondary) = ResolveGridTeachers(row.Item, _curriculumAssigneeMap);
                row.SuppressTeacherChange = true;
                row.PrimaryTeacher = primary;
                row.SecondaryTeacher = secondary;
                row.SuppressTeacherChange = false;
            }
        }
    }

    private void TeardownCurriculumGridRows()
    {
        foreach (var group in CurriculumGroups)
        {
            foreach (var row in group.Rows)
                row.PropertyChanged -= OnCurriculumGridRowPropertyChanged;
        }

        CurriculumGroups.Clear();
    }

    private List<CurriculumItem> GetCurriculumDeleteTargets()
    {
        var checkedItems = CurriculumList.Where(x => x.IsSelected).ToList();
        if (checkedItems.Count > 0)
            return checkedItems;

        if (SelectedCurriculumGridRow?.Item is { Id: var gridId })
        {
            var fromGrid = CurriculumList.FirstOrDefault(c => c.Id == gridId);
            if (fromGrid is not null)
                return [fromGrid];
        }

        if (SelectedCurriculumItem is { Id: var itemId })
        {
            var fromForm = CurriculumList.FirstOrDefault(c => c.Id == itemId);
            if (fromForm is not null)
                return [fromForm];
        }

        return [];
    }

    private void ResyncCurriculumSelectionAfterReferenceLoad(int? selectedCurriculumId)
    {
        var item = selectedCurriculumId is int id
            ? CurriculumList.FirstOrDefault(c => c.Id == id)
            : null;

        _suppressCurriculumGridSync = true;
        try
        {
            SelectedCurriculumItem = item;
        }
        finally
        {
            _suppressCurriculumGridSync = false;
        }

        SyncCurriculumGridSelection(item);
    }

    private void RefreshCurriculumGroups()
    {
        var expanded = CurriculumGroups.ToDictionary(g => g.ClassName, g => g.IsExpanded);
        TeardownCurriculumGridRows();

        foreach (var cls in ClassList.OrderBy(c => c.Grade).ThenBy(c => c.Letter, StringComparer.OrdinalIgnoreCase))
        {
            var items = CurriculumList
                .Where(c => c.ClassId == cls.Id)
                .OrderBy(c => c.SubjectName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.WeekParity)
                .ToList();
            if (items.Count == 0)
                continue;

            var group = new CurriculumClassGroup
            {
                ClassId = cls.Id,
                ClassName = cls.DisplayName,
                HeaderText = $"{cls.DisplayName} · {items.Count} предмет(ов)",
                ItemCount = items.Count,
                IsExpanded = expanded.GetValueOrDefault(cls.DisplayName, true)
            };
            foreach (var item in items)
            {
                var (primary, secondary) = ResolveGridTeachers(item, _curriculumAssigneeMap);
                var row = new CurriculumGridRow(item, primary, secondary);
                row.PropertyChanged += OnCurriculumGridRowPropertyChanged;
                group.Rows.Add(row);
            }

            group.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CurriculumClassGroup.IsExpanded))
                    NotifyCurriculumGroupsToggleState();
            };
            CurriculumGroups.Add(group);
        }

        NotifyCurriculumGroupsToggleState();
        ResyncCurriculumGridSelectionAfterRefresh();
    }

    private void ResyncCurriculumGridSelectionAfterRefresh()
    {
        if (SelectedCurriculumItem is not null)
        {
            SyncCurriculumGridSelection(SelectedCurriculumItem);
            return;
        }

        if (SelectedCurriculumGridRow is null)
            return;

        _suppressCurriculumGridSync = true;
        try
        {
            SelectedCurriculumGridRow = null;
        }
        finally
        {
            _suppressCurriculumGridSync = false;
        }
    }

    private (Teacher? primary, Teacher? secondary) ResolveGridTeachers(
        CurriculumItem item,
        IReadOnlyDictionary<int, List<int>> map)
    {
        if (!map.TryGetValue(item.Id, out var ids) || ids.Count == 0)
            return (null, null);

        var primary = TeacherList.FirstOrDefault(t => t.Id == ids[0]);
        var secondary = item.HasSubgroups && ids.Count > 1
            ? TeacherList.FirstOrDefault(t => t.Id == ids[1])
            : null;
        return (primary, secondary);
    }

    private void OnCurriculumGridRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressCurriculumGridSave || sender is not CurriculumGridRow row)
            return;

        switch (e.PropertyName)
        {
            case nameof(CurriculumGridRow.PrimaryTeacher):
            case nameof(CurriculumGridRow.SecondaryTeacher):
                if (row.SuppressTeacherChange)
                    return;
                _ = ApplyCurriculumGridTeachersAsync(row);
                break;
            case nameof(CurriculumGridRow.HoursText):
            case nameof(CurriculumGridRow.DifficultyText):
            case nameof(CurriculumGridRow.HasSubgroups):
                if (row.SuppressFieldChange)
                    return;
                _ = ApplyCurriculumGridFieldsAsync(row);
                break;
        }
    }

    private Task ApplyCurriculumGridTeachersAsync(CurriculumGridRow row) =>
        EnqueueCurriculumGridSaveAsync(row, ApplyCurriculumGridTeachersCoreAsync);

    private Task ApplyCurriculumGridFieldsAsync(CurriculumGridRow row) =>
        EnqueueCurriculumGridSaveAsync(row, ApplyCurriculumGridFieldsCoreAsync);

    private async Task EnqueueCurriculumGridSaveAsync(
        CurriculumGridRow row,
        Func<CurriculumGridRow, Task> saveCore)
    {
        var saveTask = RunCurriculumGridSaveCoreAsync(row, saveCore);
        lock (_pendingCurriculumGridSaves)
            _pendingCurriculumGridSaves.Add(saveTask);
        try
        {
            await saveTask;
        }
        finally
        {
            lock (_pendingCurriculumGridSaves)
                _pendingCurriculumGridSaves.Remove(saveTask);
        }
    }

    private async Task RunCurriculumGridSaveCoreAsync(
        CurriculumGridRow row,
        Func<CurriculumGridRow, Task> saveCore)
    {
        await _curriculumGridSaveGate.WaitAsync();
        try
        {
            await saveCore(row);
        }
        finally
        {
            _curriculumGridSaveGate.Release();
        }
    }

    private async Task ApplyCurriculumGridTeachersCoreAsync(CurriculumGridRow row)
    {
        var ids = CollectGridTeacherIds(row);
        if (!row.HasSubgroups && row.SecondaryTeacher is not null)
        {
            row.SuppressTeacherChange = true;
            row.SecondaryTeacher = null;
            row.SuppressTeacherChange = false;
            ids = CollectGridTeacherIds(row);
        }

        var before = _curriculumAssigneeMap.GetValueOrDefault(row.Item.Id)?.ToList() ?? [];
        var allowClear = ids.Count == 0;

        try
        {
            var result = await _curriculumTeacherAssignment.TrySetAssigneesAsync(
                row.Item.Id, row.Item.HasSubgroups, ids, allowClearExisting: allowClear);

            if (result == CurriculumAssigneeWriteResult.RejectedWouldClearExisting)
            {
                RestoreCurriculumGridTeachers(row, before);
                return;
            }

            _curriculumAssigneeMap[row.Item.Id] = ids.ToList();
            _saveState.MarkDirty();
            _revision.NotifyReferenceDataChanged();
            _loadedReferenceRevision = _revision.ReferenceDataRevision;

            var affected = before.Union(ids).ToHashSet();
            await _curriculumTeacherAssignment.RefreshTeachersAsync(TeacherList, affected);

            if (SelectedTeacher is not null && affected.Contains(SelectedTeacher.Id))
                RefreshCurriculumAssignmentOptions();
        }
        catch (Exception ex)
        {
            _dialogs.ShowWarning("Назначение", ex.Message);
            RestoreCurriculumGridTeachers(row, before);
        }
    }

    private static List<int> CollectGridTeacherIds(CurriculumGridRow row)
    {
        var ids = new List<int>();
        if (row.PrimaryTeacher is { Id: var primaryId })
            ids.Add(primaryId);
        if (row.HasSubgroups && row.SecondaryTeacher is { Id: var secondaryId } && secondaryId != row.PrimaryTeacher?.Id)
            ids.Add(secondaryId);
        return ids;
    }

    private async Task ApplyCurriculumGridFieldsCoreAsync(CurriculumGridRow row)
    {
        var before = SnapshotCurriculumItem(row.Item);
        if (!TryParseCurriculumHours(row.HoursText, out var hours)
            || !TryParseCurriculumDifficulty(row.DifficultyText, out var difficulty))
        {
            RestoreCurriculumGridFields(row, before);
            StatusMessage = "Укажите корректные часы (>0) и балл Сивкова (≥0)";
            return;
        }

        if (hours <= 0)
        {
            RestoreCurriculumGridFields(row, before);
            StatusMessage = "Часы в неделю должны быть больше 0";
            return;
        }

        var hadSubgroups = before.HasSubgroups;
        row.Item.HoursPerWeek = hours;
        row.Item.SubjectDifficultyScore = difficulty;
        row.Item.HasSubgroups = row.HasSubgroups;

        try
        {
            await _curriculum.UpdateAsync(row.Item);
            _saveState.MarkDirty();
            _revision.NotifyReferenceDataChanged();
            _loadedReferenceRevision = _revision.ReferenceDataRevision;

            if (hadSubgroups && !row.HasSubgroups)
                await ApplyCurriculumGridTeachersCoreAsync(row);

            if (SelectedCurriculumItem?.Id == row.Item.Id)
                LoadCurriculumForm(row.Item);
        }
        catch (Exception ex)
        {
            RestoreCurriculumGridFields(row, before);
            _dialogs.ShowWarning("Нагрузка", ex.Message);
        }
    }

    private static CurriculumItem SnapshotCurriculumItem(CurriculumItem item) => new()
    {
        Id = item.Id,
        ClassId = item.ClassId,
        SubjectId = item.SubjectId,
        HoursPerWeek = item.HoursPerWeek,
        HasSubgroups = item.HasSubgroups,
        WeekParity = item.WeekParity,
        SubjectDifficultyScore = item.SubjectDifficultyScore
    };

    private static void RestoreCurriculumGridFields(CurriculumGridRow row, CurriculumItem before)
    {
        row.Item.HoursPerWeek = before.HoursPerWeek;
        row.Item.SubjectDifficultyScore = before.SubjectDifficultyScore;
        row.Item.HasSubgroups = before.HasSubgroups;
        row.SyncFieldsFromItem();
    }

    private static bool TryParseCurriculumHours(string text, out double hours) =>
        double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out hours);

    private static bool TryParseCurriculumDifficulty(string text, out double score) =>
        double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out score) && score >= 0;

    private void RestoreCurriculumGridTeachers(CurriculumGridRow row, IReadOnlyList<int> beforeIds)
    {
        row.SuppressTeacherChange = true;
        row.PrimaryTeacher = TeacherList.FirstOrDefault(t => t.Id == beforeIds.ElementAtOrDefault(0));
        row.SecondaryTeacher = row.HasSubgroups
            ? TeacherList.FirstOrDefault(t => t.Id == beforeIds.ElementAtOrDefault(1))
            : null;
        row.SuppressTeacherChange = false;
    }

    private static CurriculumItem? ResolveCurriculumRowItem(object? parameter) =>
        parameter switch
        {
            CurriculumGridRow row => row.Item,
            CurriculumItem item => item,
            _ => null
        };

    private void SelectCurriculumRowForEdit(object? parameter)
    {
        var item = ResolveCurriculumRowItem(parameter);
        if (item is not null)
            SelectedCurriculumItem = item;
    }

    private void SyncCurriculumGridSelection(CurriculumItem? item)
    {
        var row = item is null ? null : FindCurriculumGridRow(item.Id);
        if (row == SelectedCurriculumGridRow)
            return;

        _suppressCurriculumGridSync = true;
        try
        {
            SelectedCurriculumGridRow = row;
        }
        finally
        {
            _suppressCurriculumGridSync = false;
        }
    }

    private CurriculumGridRow? FindCurriculumGridRow(int itemId)
    {
        foreach (var group in CurriculumGroups)
        {
            foreach (var row in group.Rows)
            {
                if (row.Item.Id == itemId)
                    return row;
            }
        }

        return null;
    }
}
