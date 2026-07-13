using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Текст одной ячейки слота для экспорта сводки в Excel.</summary>
public static class OverviewScheduleExcelCellText
{
    public static string FormatSlot(OverviewTimelineCell cell)
    {
        if (cell.IsBreakColumn)
            return "перемена";

        if (cell.IsDynamicPauseColumn)
            return "дин. пауза";

        if (!cell.HasContent)
            return "";

        return string.Join(
            "\n\n",
            cell.Parts
                .Select(FormatPart)
                .Where(text => text.Length > 0));
    }

    private static string FormatPart(OverviewTimelinePart part)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(part.SubgroupLabel))
            lines.Add($"[{part.SubgroupLabel}]");
        if (!string.IsNullOrWhiteSpace(part.SubjectLine))
            lines.Add(part.SubjectLine);

        if (!string.IsNullOrWhiteSpace(part.SecondaryLine))
            lines.Add(part.SecondaryLine);
        else
        {
            if (!string.IsNullOrWhiteSpace(part.TeacherLine))
                lines.Add(part.TeacherLine);
            if (!string.IsNullOrWhiteSpace(part.RoomLine))
                lines.Add(part.RoomLine);
        }

        return string.Join("\n", lines);
    }
}
