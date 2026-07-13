using ArmZavuch.Models;
using ClosedXML.Excel;

namespace ArmZavuch.Services.Export;

/// <summary>Запись одного листа сводки: отдельная ячейка на каждый урок/перемену/паузу.</summary>
public static class OverviewScheduleExcelWriter
{
    private const int DayCount = 6;

    public static void WriteSheet(
        XLWorkbook workbook,
        OverviewScheduleExportSheet sheet,
        string schoolName,
        string templateLabel,
        DateOnly exportedOn)
    {
        var ws = workbook.Worksheets.Add(SheetName(sheet.SheetTitle));
        var layout = OverviewScheduleExcelLayout.Create(sheet);
        var dataRows = sheet.Rows
            .Where(row => !row.IsSectionHeader && !row.IsDayHeaderRow)
            .ToList();

        ws.Cell(1, 1).Value = schoolName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value =
            $"{sheet.SheetTitle} · шаблон «{templateLabel}» · {exportedOn:dd.MM.yyyy}";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#64748B");

        var dayHeaderRow = 4;
        var slotHeaderRow = 5;
        WriteColumnHeaders(ws, layout, sheet.RowHeader, dataRows, dayHeaderRow, slotHeaderRow);

        var rowIndex = slotHeaderRow + 1;
        foreach (var matrixRow in sheet.Rows)
        {
            if (matrixRow.IsDayHeaderRow)
                continue;

            if (matrixRow.IsSectionHeader)
            {
                ws.Cell(rowIndex, 1).Value = matrixRow.SectionTitle ?? "";
                var sectionRange = ws.Range(rowIndex, 1, rowIndex, layout.TotalColumns);
                sectionRange.Merge();
                sectionRange.Style.Font.Bold = true;
                sectionRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                sectionRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                rowIndex++;
                continue;
            }

            ws.Cell(rowIndex, 1).Value = FormatRowLabel(matrixRow);
            ws.Cell(rowIndex, 1).Style.Alignment.WrapText = true;
            ws.Cell(rowIndex, 1).Style.Font.Bold = true;
            ws.Cell(rowIndex, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

            for (var dayIndex = 0; dayIndex < DayCount; dayIndex++)
            {
                var day = dayIndex < matrixRow.Days.Count ? matrixRow.Days[dayIndex] : null;
                for (var slotIndex = 0; slotIndex < layout.SlotsForDay(dayIndex); slotIndex++)
                {
                    var column = layout.ColumnForSlot(dayIndex, slotIndex);
                    var excelCell = ws.Cell(rowIndex, column);
                    if (day is null || slotIndex >= day.Lessons.Count)
                        continue;

                    var slot = day.Lessons[slotIndex];
                    excelCell.Value = OverviewScheduleExcelCellText.FormatSlot(slot);
                    excelCell.Style.Alignment.WrapText = true;
                    excelCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    excelCell.Style.Font.FontSize = 10;
                    ApplySlotStyle(excelCell, slot);
                }
            }

            ws.Row(rowIndex).AdjustToContents();
            if (ws.Row(rowIndex).Height < 42)
                ws.Row(rowIndex).Height = 42;

            rowIndex++;
        }

        ws.Column(1).Width = 20;
        for (var column = 2; column <= layout.TotalColumns; column++)
            ws.Column(column).Width = 14;

        ws.SheetView.FreezeRows(slotHeaderRow);
        ws.SheetView.FreezeColumns(1);
    }

    private static void WriteColumnHeaders(
        IXLWorksheet ws,
        OverviewScheduleExcelLayout layout,
        string rowHeader,
        IReadOnlyList<OverviewMatrixRow> dataRows,
        int dayHeaderRow,
        int slotHeaderRow)
    {
        ws.Cell(dayHeaderRow, 1).Value = rowHeader;
        ws.Range(dayHeaderRow, 1, slotHeaderRow, 1).Merge();
        ws.Cell(dayHeaderRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Cell(dayHeaderRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        for (var dayIndex = 0; dayIndex < DayCount; dayIndex++)
        {
            var slots = layout.SlotsForDay(dayIndex);
            var startColumn = layout.ColumnForSlot(dayIndex, 0);
            var dayLabel = dayIndex < layout.DayHeaders.Count
                ? layout.DayHeaders[dayIndex].Label
                : $"Д{dayIndex + 1}";

            ws.Cell(dayHeaderRow, startColumn).Value = dayLabel;
            if (slots > 1)
            {
                ws.Range(dayHeaderRow, startColumn, dayHeaderRow, startColumn + slots - 1).Merge();
            }

            for (var slotIndex = 0; slotIndex < slots; slotIndex++)
            {
                var column = layout.ColumnForSlot(dayIndex, slotIndex);
                ws.Cell(slotHeaderRow, column).Value = layout.SlotHeaderLabel(dataRows, dayIndex, slotIndex);
            }
        }

        var headerRange = ws.Range(dayHeaderRow, 1, slotHeaderRow, layout.TotalColumns);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        ws.Row(slotHeaderRow).Height = 28;
    }

    private static void ApplySlotStyle(IXLCell cell, OverviewTimelineCell slot)
    {
        if (slot.IsBreakColumn)
        {
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF8E7");
            cell.Style.Font.FontColor = XLColor.FromHtml("#B45309");
            return;
        }

        if (slot.IsDynamicPauseColumn)
        {
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#ECFDF3");
            cell.Style.Font.FontColor = XLColor.FromHtml("#15803D");
        }
    }

    private static string FormatRowLabel(OverviewMatrixRow row) =>
        string.IsNullOrWhiteSpace(row.RowSubLabel)
            ? row.RowLabel
            : $"{row.RowLabel}\n{row.RowSubLabel}";

    private static string SheetName(string title)
    {
        var trimmed = title.Trim();
        return trimmed.Length <= 31 ? trimmed : trimmed[..31];
    }
}
