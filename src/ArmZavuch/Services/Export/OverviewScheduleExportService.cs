using ArmZavuch.Models;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace ArmZavuch.Services.Export;

/// <summary>Лист сводки для экспорта в Excel.</summary>
public sealed record OverviewScheduleExportSheet(
    string SheetTitle,
    string RowHeader,
    IReadOnlyList<OverviewColumnHeader> Columns,
    IReadOnlyList<OverviewMatrixRow> Rows);

/// <summary>Экспорт сводки расписания: 4 листа Excel (ТЗ §7.2).</summary>
public sealed class OverviewScheduleExportService
{
    public bool TryExport(string schoolName, string templateLabel, IReadOnlyList<OverviewScheduleExportSheet> sheets)
    {
        if (sheets.Count == 0)
            return false;

        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Сводка_{SanitizeFileName(templateLabel)}_{DateTime.Today:yyyy-MM-dd}.xlsx"
        };
        if (dlg.ShowDialog() != true)
            return false;

        using var workbook = new XLWorkbook();
        var exportedOn = DateOnly.FromDateTime(DateTime.Today);
        foreach (var sheet in sheets)
            OverviewScheduleExcelWriter.WriteSheet(workbook, sheet, schoolName, templateLabel, exportedOn);

        workbook.SaveAs(dlg.FileName);
        return true;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "расписание";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch));
        return string.IsNullOrWhiteSpace(sanitized) ? "расписание" : sanitized;
    }
}
