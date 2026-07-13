using ArmZavuch.Models;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace ArmZavuch.Services.Export;

/// <summary>Выгрузка журнала замен и отсутствий в Excel: листы данных и сводка.</summary>
public static class StaffJournalExcelExporter
{
    public static void Export(
        DateOnly from,
        DateOnly to,
        string schoolName,
        IReadOnlyList<SubstitutionRecord> substitutions,
        IReadOnlyList<AbsenceHistoryRow> absences,
        IReadOnlyList<StaffActivitySummaryRow> summary)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Журнал_{from:yyyy-MM}_{to:yyyy-MM}.xlsx"
        };
        if (dlg.ShowDialog() != true)
            return;

        using var wb = new XLWorkbook();
        WriteSubstitutionsSheet(wb, from, to, schoolName, substitutions);
        WriteAbsencesSheet(wb, from, to, schoolName, absences);
        WriteSummarySheet(wb, from, to, schoolName, summary, substitutions, absences);
        wb.SaveAs(dlg.FileName);
    }

    private static void WriteSubstitutionsSheet(
        XLWorkbook wb, DateOnly from, DateOnly to, string schoolName, IReadOnlyList<SubstitutionRecord> lines)
    {
        var ws = wb.Worksheets.Add("Замены");
        ws.Cell(1, 1).Value = schoolName;
        ws.Cell(2, 1).Value = $"Замены · {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
        WriteHeaders(ws, 4, "Дата", "Урок", "Время", "Смена", "Класс", "Предмет",
            "Отсутствовал", "Заменял", "Официально", "Заметка");

        var row = 5;
        foreach (var line in lines)
        {
            ws.Cell(row, 1).Value = FormatDate(line.Date);
            ws.Cell(row, 2).Value = line.LessonNumber;
            ws.Cell(row, 3).Value = line.TimeDisplay;
            ws.Cell(row, 4).Value = line.ShiftDisplay;
            ws.Cell(row, 5).Value = line.ClassName;
            ws.Cell(row, 6).Value = line.SubjectName;
            ws.Cell(row, 7).Value = line.AbsentTeacherName;
            ws.Cell(row, 8).Value = line.ReplacementTeacherName;
            ws.Cell(row, 9).Value = line.IsOfficial ? "да" : "нет";
            ws.Cell(row, 10).Value = line.Note ?? "";
            row++;
        }

        WriteTotals(ws, row + 1, "Итого замен:", lines.Count);
        ws.Cell(row + 2, 1).Value = "Официальных:";
        ws.Cell(row + 2, 2).Value = lines.Count(l => l.IsOfficial);
        ws.Cell(row + 3, 1).Value = "Неофициальных:";
        ws.Cell(row + 3, 2).Value = lines.Count(l => !l.IsOfficial);
        ws.Row(row + 2).Style.Font.Bold = true;
        ws.Row(row + 3).Style.Font.Bold = true;

        var byReplacement = lines
            .GroupBy(l => l.ReplacementTeacherName)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

        var statRow = row + 5;
        ws.Cell(statRow, 1).Value = "По заменяющим";
        ws.Row(statRow).Style.Font.Bold = true;
        statRow++;
        WriteHeaders(ws, statRow, "Заменяющий", "Официально", "Неофициально", "Всего");
        statRow++;

        foreach (var group in byReplacement)
        {
            ws.Cell(statRow, 1).Value = group.Key;
            ws.Cell(statRow, 2).Value = group.Count(l => l.IsOfficial);
            ws.Cell(statRow, 3).Value = group.Count(l => !l.IsOfficial);
            ws.Cell(statRow, 4).Value = group.Count();
            statRow++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteAbsencesSheet(
        XLWorkbook wb, DateOnly from, DateOnly to, string schoolName, IReadOnlyList<AbsenceHistoryRow> lines)
    {
        var ws = wb.Worksheets.Add("Отсутствия");
        ws.Cell(1, 1).Value = schoolName;
        ws.Cell(2, 1).Value = $"Отсутствия · {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
        WriteHeaders(ws, 4, "Учитель", "Причина", "С", "По", "Дней в периоде",
            "Официально", "Заметка");

        var row = 5;
        foreach (var line in lines)
        {
            ws.Cell(row, 1).Value = line.TeacherName;
            ws.Cell(row, 2).Value = line.StatusLabel;
            ws.Cell(row, 3).Value = FormatDate(line.StartDate);
            ws.Cell(row, 4).Value = string.IsNullOrWhiteSpace(line.EndDate)
                ? "…"
                : FormatDate(line.EndDate);
            ws.Cell(row, 5).Value = line.DaysInRange;
            ws.Cell(row, 6).Value = line.IsOfficial ? "да" : "нет";
            ws.Cell(row, 7).Value = line.Note ?? "";
            row++;
        }

        WriteTotals(ws, row + 1, "Итого периодов:", lines.Count);
        ws.Columns().AdjustToContents();
    }

    private static void WriteSummarySheet(
        XLWorkbook wb,
        DateOnly from,
        DateOnly to,
        string schoolName,
        IReadOnlyList<StaffActivitySummaryRow> summary,
        IReadOnlyList<SubstitutionRecord> substitutions,
        IReadOnlyList<AbsenceHistoryRow> absences)
    {
        var ws = wb.Worksheets.Add("Сводка");
        ws.Cell(1, 1).Value = schoolName;
        ws.Cell(2, 1).Value = $"Сводка · {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
        ws.Cell(3, 1).Value =
            "Диаграммы: выделите блок «Топ отсутствующих» или «Топ заменяющих» → Вставка → Гистограмма.";

        var row = 5;
        WriteHeaders(ws, row, "Педагог", "Дней отсутствия", "Больничный", "Отгул", "Прочее",
            "Замен всего", "Офиц.", "Неофиц.", "Его заменяли");
        row++;

        foreach (var line in summary)
        {
            ws.Cell(row, 1).Value = line.TeacherName;
            ws.Cell(row, 2).Value = line.AbsenceDays;
            ws.Cell(row, 3).Value = line.SickDays;
            ws.Cell(row, 4).Value = line.LeaveDays;
            ws.Cell(row, 5).Value = line.OtherDays;
            ws.Cell(row, 6).Value = line.SubstitutionCount;
            ws.Cell(row, 7).Value = line.OfficialSubstitutionCount;
            ws.Cell(row, 8).Value = line.UnofficialSubstitutionCount;
            ws.Cell(row, 9).Value = line.WasReplacedCount;
            row++;
        }

        var totalsRow = row + 1;
        ws.Cell(totalsRow, 1).Value = "Итого замен в журнале:";
        ws.Cell(totalsRow, 2).Value = substitutions.Count;
        ws.Cell(totalsRow + 1, 1).Value = "Официальных:";
        ws.Cell(totalsRow + 1, 2).Value = substitutions.Count(s => s.IsOfficial);
        ws.Cell(totalsRow + 2, 1).Value = "Неофициальных:";
        ws.Cell(totalsRow + 2, 2).Value = substitutions.Count(s => !s.IsOfficial);
        ws.Cell(totalsRow + 3, 1).Value = "Периодов отсутствия:";
        ws.Cell(totalsRow + 3, 2).Value = absences.Count;
        ws.Row(totalsRow).Style.Font.Bold = true;
        ws.Row(totalsRow + 1).Style.Font.Bold = true;
        ws.Row(totalsRow + 2).Style.Font.Bold = true;
        ws.Row(totalsRow + 3).Style.Font.Bold = true;

        var chartRow = totalsRow + 5;
        ws.Cell(chartRow, 1).Value = "Топ отсутствующих";
        ws.Row(chartRow).Style.Font.Bold = true;
        chartRow++;
        ws.Cell(chartRow, 1).Value = "Педагог";
        ws.Cell(chartRow, 2).Value = "Дней";
        ws.Row(chartRow).Style.Font.Bold = true;
        chartRow++;

        var topAbsent = summary.Where(s => s.AbsenceDays > 0).Take(10).ToList();
        foreach (var item in topAbsent)
        {
            ws.Cell(chartRow, 1).Value = item.TeacherName;
            ws.Cell(chartRow, 2).Value = item.AbsenceDays;
            chartRow++;
        }

        chartRow += 2;
        ws.Cell(chartRow, 1).Value = "Топ заменяющих";
        ws.Row(chartRow).Style.Font.Bold = true;
        chartRow++;
        ws.Cell(chartRow, 1).Value = "Педагог";
        ws.Cell(chartRow, 2).Value = "Официально";
        ws.Cell(chartRow, 3).Value = "Неофициально";
        ws.Cell(chartRow, 4).Value = "Всего";
        ws.Row(chartRow).Style.Font.Bold = true;
        chartRow++;

        var topSub = summary.Where(s => s.SubstitutionCount > 0).Take(10).ToList();
        foreach (var item in topSub)
        {
            ws.Cell(chartRow, 1).Value = item.TeacherName;
            ws.Cell(chartRow, 2).Value = item.OfficialSubstitutionCount;
            ws.Cell(chartRow, 3).Value = item.UnofficialSubstitutionCount;
            ws.Cell(chartRow, 4).Value = item.SubstitutionCount;
            chartRow++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteHeaders(IXLWorksheet ws, int row, params string[] headers)
    {
        ws.Row(row).Style.Font.Bold = true;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(row, c + 1).Value = headers[c];
    }

    private static void WriteTotals(IXLWorksheet ws, int row, string label, int value)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 2).Value = value;
        ws.Row(row).Style.Font.Bold = true;
    }

    private static string FormatDate(string iso) =>
        DateOnly.TryParse(iso, out var d) ? d.ToString("dd.MM.yyyy") : iso;
}
