using ArmZavuch.Models;
using ArmZavuch.Services.Staff;

namespace ArmZavuch.ViewModels;

/// <summary>Синхронизация галочек нагрузки с предметами и классами в анкете сотрудника.</summary>
public sealed partial class DirectoriesViewModel
{
    private bool _suppressCurriculumProfileSync;
    private int? _curriculumUiTeacherId;

    private HashSet<int> CollectCurriculumSelectionSnapshot()
    {
        if (SelectedTeacher is null)
            return [];

        if (CurriculumAssignmentSections.Count > 0 && _curriculumUiTeacherId == SelectedTeacher.Id)
        {
            return CurriculumAssignmentSections
                .SelectMany(s => s.Items)
                .Where(o => o.IsSelected)
                .Select(o => o.CurriculumId)
                .ToHashSet();
        }

        return SelectedTeacher.CurriculumAssignments
            .Select(x => x.CurriculumId)
            .ToHashSet();
    }

    private void OnCurriculumAssignmentSelectionChanged(CurriculumPreferenceItem option, bool isSelected)
    {
        NotifyTeacherCurriculumTotalChanged();
        if (_suppressCurriculumProfileSync || SelectedTeacher is null)
            return;

        var item = CurriculumList.FirstOrDefault(c => c.Id == option.CurriculumId);
        if (item is null)
            return;

        if (isSelected && IsCurriculumBlockedForNewAssignee(option))
        {
            _suppressCurriculumProfileSync = true;
            try
            {
                option.IsSelected = false;
            }
            finally
            {
                _suppressCurriculumProfileSync = false;
            }

            return;
        }

        _suppressCurriculumProfileSync = true;
        try
        {
            if (isSelected)
                ApplyCurriculumSelectionToProfile(item);
            else
                RemoveCurriculumSelectionFromProfile(item);
        }
        finally
        {
            _suppressCurriculumProfileSync = false;
        }

        _ = PersistCurriculumAssignmentChangeAsync(option, isSelected);
    }

    private async Task PersistCurriculumAssignmentChangeAsync(
        CurriculumPreferenceItem option,
        bool isSelected)
    {
        if (SelectedTeacher is null)
            return;

        var item = CurriculumList.FirstOrDefault(c => c.Id == option.CurriculumId);
        if (item is null)
            return;

        await _curriculumGridSaveGate.WaitAsync();
        try
        {
            var before = await _teachers.GetExplicitAssigneesForCurriculumAsync(item.Id);
            var updated = before.Where(id => id != SelectedTeacher.Id).ToList();
            if (isSelected)
                updated.Add(SelectedTeacher.Id);

            var normalized = CurriculumTeacherAssignmentRules.Normalize(item.HasSubgroups, updated);
            if (normalized.SequenceEqual(before.OrderBy(id => id)))
                return;

            var result = await _curriculumTeacherAssignment.TrySetAssigneesAsync(
                item.Id, item.HasSubgroups, normalized, allowClearExisting: !isSelected);

            if (result == CurriculumAssigneeWriteResult.RejectedWouldClearExisting)
            {
                _suppressCurriculumProfileSync = true;
                try
                {
                    option.IsSelected = !isSelected;
                }
                finally
                {
                    _suppressCurriculumProfileSync = false;
                }
                return;
            }
            _curriculumAssigneeMap[item.Id] = normalized.ToList();
            _saveState.MarkDirty();
            _revision.NotifyReferenceDataChanged();
            _loadedReferenceRevision = _revision.ReferenceDataRevision;

            await _teachers.RefreshCurriculumAssignmentsAsync(SelectedTeacher);
            RefreshCurriculumGroupsFromAssigneeMap();

            _suppressCurriculumProfileSync = true;
            try
            {
                RefreshCurriculumAssignmentOptions();
            }
            finally
            {
                _suppressCurriculumProfileSync = false;
            }
        }
        catch (Exception ex)
        {
            _dialogs.ShowWarning("Назначение", ex.Message);
            _suppressCurriculumProfileSync = true;
            try
            {
                option.IsSelected = !isSelected;
            }
            finally
            {
                _suppressCurriculumProfileSync = false;
            }
        }
        finally
        {
            _curriculumGridSaveGate.Release();
        }
    }

    private void ApplyCurriculumSelectionToProfile(CurriculumItem item)
    {
        EnsureSubjectInProfile(item.SubjectName);
        EnsurePreferredClassSelected(item.ClassId);
    }

    private void RemoveCurriculumSelectionFromProfile(CurriculumItem item)
    {
        if (!IsSubjectUsedInSelectedCurriculum(item.SubjectName, item.Id))
            RemoveSubjectFromProfile(item.SubjectName);

        if (!IsClassUsedInSelectedCurriculum(item.ClassId, item.Id))
            RemovePreferredClass(item.ClassId);
    }

    private void EnsureSubjectInProfile(string subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return;

        var primary = EditPrimarySubjectItem?.Name ?? SelectedTeacher?.PrimarySubject;
        if (!string.IsNullOrWhiteSpace(primary)
            && subjectName.Equals(primary.Trim(), StringComparison.OrdinalIgnoreCase))
            return;

        if (string.IsNullOrWhiteSpace(primary) && EditPrimarySubjectItem is null)
        {
            var asPrimary = FindSubjectByName(subjectName);
            if (asPrimary is not null)
            {
                EditPrimarySubjectItem = asPrimary;
                RefreshSecondarySubjectOptions();
                return;
            }
        }

        var secondary = SecondarySubjectOptions.FirstOrDefault(o =>
            o.DisplayName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
        if (secondary is not null && !secondary.IsSelected)
            secondary.IsSelected = true;
    }

    private void RemoveSubjectFromProfile(string subjectName)
    {
        var primary = EditPrimarySubjectItem?.Name ?? SelectedTeacher?.PrimarySubject;
        if (!string.IsNullOrWhiteSpace(primary)
            && subjectName.Equals(primary.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            EditPrimarySubjectItem = null;
            RefreshSecondarySubjectOptions();
            return;
        }

        var secondary = SecondarySubjectOptions.FirstOrDefault(o =>
            o.DisplayName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
        if (secondary is not null)
            secondary.IsSelected = false;
    }

    private void EnsurePreferredClassSelected(int classId)
    {
        var cls = ClassPreferenceOptions.FirstOrDefault(o => o.ClassId == classId);
        if (cls is not null && !cls.IsSelected)
            cls.IsSelected = true;
    }

    private void RemovePreferredClass(int classId)
    {
        var cls = ClassPreferenceOptions.FirstOrDefault(o => o.ClassId == classId);
        if (cls is not null)
            cls.IsSelected = false;
    }

    private bool IsSubjectUsedInSelectedCurriculum(string subjectName, int excludeCurriculumId) =>
        GetSelectedCurriculumItems(excludeCurriculumId)
            .Any(c => c.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));

    private bool IsClassUsedInSelectedCurriculum(int classId, int excludeCurriculumId) =>
        GetSelectedCurriculumItems(excludeCurriculumId).Any(c => c.ClassId == classId);

    private static bool IsCurriculumBlockedForNewAssignee(CurriculumPreferenceItem option) =>
        option.HasSubgroups
            ? option.OtherTeacherNames.Count >= 2
            : option.OtherTeacherNames.Count > 0;

    private IEnumerable<CurriculumItem> GetSelectedCurriculumItems(int excludeCurriculumId)
    {
        var selectedIds = CurriculumAssignmentSections
            .SelectMany(s => s.Items)
            .Where(o => o.IsSelected && o.CurriculumId != excludeCurriculumId)
            .Select(o => o.CurriculumId)
            .ToHashSet();
        return CurriculumList.Where(c => selectedIds.Contains(c.Id));
    }
}
