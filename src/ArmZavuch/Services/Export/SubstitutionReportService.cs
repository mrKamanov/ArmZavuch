using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Журнал замен и отсутствий за период: загрузка данных и выгрузка в Excel.</summary>
public sealed class SubstitutionReportService
{
    private readonly SubstitutionRecordRepository _records;
    private readonly TeacherStatusRepository _statuses;
    private readonly TeacherRepository _teachers;

    public SubstitutionReportService(
        SubstitutionRecordRepository records,
        TeacherStatusRepository statuses,
        TeacherRepository teachers)
    {
        _records = records;
        _statuses = statuses;
        _teachers = teachers;
    }

    public async Task<StaffJournalReportBundle> LoadAsync(DateOnly from, DateOnly to)
    {
        var substitutions = await _records.GetForDateRangeAsync(from, to);
        var periods = await _statuses.GetOverlappingRangeAsync(from, to);
        var teachers = await _teachers.GetAllAsync();
        var byId = teachers.ToDictionary(t => t.Id);

        var absences = StaffActivityReportBuilder.BuildAbsenceRows(periods, byId, from, to);
        var summary = StaffActivityReportBuilder.BuildSummary(periods, teachers, substitutions, from, to);

        return new StaffJournalReportBundle
        {
            Substitutions = substitutions,
            Absences = absences,
            Summary = summary,
            TopAbsenteesChart = StaffActivityReportBuilder.BuildAbsenteeChart(summary),
            TopSubstitutorsChart = StaffActivityReportBuilder.BuildSubstitutorChart(summary)
        };
    }

    public void ExportExcel(DateOnly from, DateOnly to, string schoolName, StaffJournalReportBundle bundle) =>
        StaffJournalExcelExporter.Export(
            from, to, schoolName,
            bundle.Substitutions,
            bundle.Absences,
            bundle.Summary);
}
