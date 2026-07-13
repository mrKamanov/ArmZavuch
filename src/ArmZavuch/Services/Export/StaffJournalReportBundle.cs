using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Данные журнала за период: замены, отсутствия, сводка и точки диаграмм.</summary>
public sealed class StaffJournalReportBundle
{
    public List<SubstitutionRecord> Substitutions { get; init; } = [];
    public List<AbsenceHistoryRow> Absences { get; init; } = [];
    public List<StaffActivitySummaryRow> Summary { get; init; } = [];
    public List<StaffBarChartPoint> TopAbsenteesChart { get; init; } = [];
    public List<StaffBarChartPoint> TopSubstitutorsChart { get; init; } = [];

    public int OfficialSubstitutionTotal => Substitutions.Count(s => s.IsOfficial);

    public int UnofficialSubstitutionTotal => Substitutions.Count - OfficialSubstitutionTotal;

    public string SubstitutionTotalsLine =>
        Substitutions.Count == 0
            ? "Замен за период нет"
            : $"Замен: {Substitutions.Count} · официально: {OfficialSubstitutionTotal} · неофициально: {UnofficialSubstitutionTotal}";
}
