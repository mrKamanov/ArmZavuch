using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Колоночная сетка Excel: один столбец на каждый слот дня (урок, перемена, пауза).</summary>
public sealed class OverviewScheduleExcelLayout
{
    private readonly int[] _slotsPerDay;
    private readonly int[] _dayStartColumn;

    public int TotalColumns { get; }
    public IReadOnlyList<OverviewColumnHeader> DayHeaders { get; }

    private OverviewScheduleExcelLayout(
        IReadOnlyList<OverviewColumnHeader> dayHeaders,
        int[] slotsPerDay,
        int[] dayStartColumn,
        int totalColumns)
    {
        DayHeaders = dayHeaders;
        _slotsPerDay = slotsPerDay;
        _dayStartColumn = dayStartColumn;
        TotalColumns = totalColumns;
    }

    public static OverviewScheduleExcelLayout Create(OverviewScheduleExportSheet sheet)
    {
        const int dayCount = 6;
        var dataRows = sheet.Rows
            .Where(row => !row.IsSectionHeader && !row.IsDayHeaderRow)
            .ToList();

        var slotsPerDay = new int[dayCount];
        for (var dayIndex = 0; dayIndex < dayCount; dayIndex++)
        {
            var max = dataRows
                .Where(row => dayIndex < row.Days.Count)
                .Select(row => row.Days[dayIndex].Lessons.Count)
                .DefaultIfEmpty(0)
                .Max();
            slotsPerDay[dayIndex] = Math.Max(max, 1);
        }

        var dayStartColumn = new int[dayCount];
        var column = 2;
        for (var dayIndex = 0; dayIndex < dayCount; dayIndex++)
        {
            dayStartColumn[dayIndex] = column;
            column += slotsPerDay[dayIndex];
        }

        return new OverviewScheduleExcelLayout(
            sheet.Columns,
            slotsPerDay,
            dayStartColumn,
            column - 1);
    }

    public int SlotsForDay(int dayIndex) => _slotsPerDay[dayIndex];

    public int ColumnForSlot(int dayIndex, int slotIndex) => _dayStartColumn[dayIndex] + slotIndex;

    public string SlotHeaderLabel(IReadOnlyList<OverviewMatrixRow> dataRows, int dayIndex, int slotIndex)
    {
        foreach (var row in dataRows)
        {
            if (dayIndex >= row.Days.Count)
                continue;

            var lessons = row.Days[dayIndex].Lessons;
            if (slotIndex >= lessons.Count)
                continue;

            var label = lessons[slotIndex].TimeLabel?.Trim();
            if (!string.IsNullOrWhiteSpace(label))
                return label;
        }

        return $"Слот {slotIndex + 1}";
    }
}
