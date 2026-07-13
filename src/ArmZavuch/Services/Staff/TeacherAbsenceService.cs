using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Staff;

/// <summary>Единый учёт отсутствий: быстрая отметка, период, открытый больничный.</summary>
public sealed class TeacherAbsenceService
{
    private readonly TeacherStatusRepository _statuses;
    private readonly TeacherRepository _teachers;

    public TeacherAbsenceService(TeacherStatusRepository statuses, TeacherRepository teachers)
    {
        _statuses = statuses;
        _teachers = teachers;
    }

    public Task<int> MarkQuickAsync(
        int teacherId, DateOnly date, string statusType, string? note,
        bool isOfficial = false) =>
        UpsertConsolidatedAsync(teacherId, date, date, statusType, note, isOfficial, AbsenceSources.Dispatcher);

    public Task<int> MarkPeriodAsync(
        int teacherId, DateOnly start, DateOnly? end, string statusType, string? note,
        bool isOfficial, string source)
    {
        if (end is not null && end.Value < start)
            throw new InvalidOperationException("Дата окончания раньше начала");

        return UpsertConsolidatedAsync(teacherId, start, end, statusType, note, isOfficial, source);
    }

    public async Task ClosePeriodAsync(int periodId, DateOnly endDate) =>
        await _statuses.UpdateEndDateAsync(periodId, endDate.ToString("yyyy-MM-dd"));

    public async Task DeletePeriodAsync(int periodId) =>
        await _statuses.DeleteAsync(periodId);

    public async Task<bool> CancelForDateAsync(int teacherId, DateOnly date)
    {
        var active = await _statuses.GetActiveForTeacherOnDateAsync(teacherId, date);
        if (active is null)
            return false;

        var start = DateOnly.Parse(active.StartDate);
        var endIso = active.EndDate;
        var isSingleDay = !active.IsOpen && start == DateOnly.Parse(endIso!);
        if (isSingleDay && start == date)
        {
            await _statuses.DeleteAsync(active.Id);
            return true;
        }

        var previous = date.AddDays(-1);
        if (previous < start)
        {
            await _statuses.DeleteAsync(active.Id);
            return true;
        }

        await _statuses.UpdateEndDateAsync(active.Id, previous.ToString("yyyy-MM-dd"));
        return true;
    }

    public async Task<List<TeacherAbsenceListItem>> GetAbsentForDateAsync(DateOnly date)
    {
        await RepairDuplicatesForDateAsync(date);

        var statuses = await _statuses.GetActiveForDateAsync(date);
        var teachers = await _teachers.GetAllAsync();
        var byId = teachers.ToDictionary(t => t.Id);

        return statuses
            .Where(s => byId.ContainsKey(s.TeacherId))
            .GroupBy(s => s.TeacherId)
            .Select(g => g.OrderByDescending(p => p.Id).First())
            .Select(s =>
            {
                var teacher = byId[s.TeacherId];
                return new TeacherAbsenceListItem
                {
                    PeriodId = s.Id,
                    TeacherId = s.TeacherId,
                    TeacherName = teacher.FullName,
                    StatusLabel = $"{StaffStatusTypes.ToIcon(s.AbsenceNoteText)}{StaffStatusTypes.ToDisplay(s.StatusType)}",
                    PeriodText = s.DateRangeDisplay,
                    SourceText = AbsenceSources.ToDisplay(s.Source),
                    IsOfficial = s.IsOfficial,
                    IsOpen = s.IsOpen
                };
            })
            .OrderBy(i => i.TeacherName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public Task<TeacherStatusPeriod?> GetActiveForTeacherOnDateAsync(int teacherId, DateOnly date) =>
        _statuses.GetActiveForTeacherOnDateAsync(teacherId, date);

    private async Task<int> UpsertConsolidatedAsync(
        int teacherId, DateOnly start, DateOnly? end, string statusType, string? note,
        bool isOfficial, string source)
    {
        var existing = await _statuses.GetForTeacherAsync(teacherId);
        var overlapping = existing
            .Where(p => Overlaps(
                DateOnly.Parse(p.StartDate),
                p.IsOpen ? null : DateOnly.Parse(p.EndDate!),
                start,
                end))
            .ToList();

        var mergedStart = start;
        DateOnly? mergedEnd = end;
        foreach (var period in overlapping)
        {
            var periodStart = DateOnly.Parse(period.StartDate);
            if (periodStart < mergedStart)
                mergedStart = periodStart;

            if (period.IsOpen)
                mergedEnd = null;
            else if (mergedEnd is not null)
            {
                var periodEnd = DateOnly.Parse(period.EndDate!);
                if (periodEnd > mergedEnd)
                    mergedEnd = periodEnd;
            }

            await _statuses.DeleteAsync(period.Id);
        }

        return await _statuses.InsertAsync(new TeacherStatusPeriod
        {
            TeacherId = teacherId,
            StatusType = statusType,
            StartDate = mergedStart.ToString("yyyy-MM-dd"),
            EndDate = mergedEnd?.ToString("yyyy-MM-dd"),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IsOfficial = isOfficial,
            Source = source
        });
    }

    private async Task RepairDuplicatesForDateAsync(DateOnly date)
    {
        var active = await _statuses.GetActiveForDateAsync(date);
        foreach (var group in active.GroupBy(s => s.TeacherId).Where(g => g.Count() > 1))
        {
            var items = group.ToList();
            var latest = items.MaxBy(p => p.Id)!;
            var start = items.Min(p => DateOnly.Parse(p.StartDate));
            DateOnly? end = items.Any(p => p.IsOpen)
                ? null
                : items.Max(p => DateOnly.Parse(p.EndDate!));

            foreach (var period in items)
                await _statuses.DeleteAsync(period.Id);

            await _statuses.InsertAsync(new TeacherStatusPeriod
            {
                TeacherId = latest.TeacherId,
                StatusType = latest.StatusType,
                StartDate = start.ToString("yyyy-MM-dd"),
                EndDate = end?.ToString("yyyy-MM-dd"),
                Note = latest.Note,
                IsOfficial = items.Any(p => p.IsOfficial),
                Source = latest.Source
            });
        }
    }

    private static bool Overlaps(DateOnly aStart, DateOnly? aEnd, DateOnly bStart, DateOnly? bEnd)
    {
        var aEndEff = aEnd ?? DateOnly.MaxValue;
        var bEndEff = bEnd ?? DateOnly.MaxValue;
        return aStart <= bEndEff && bStart <= aEndEff;
    }
}
