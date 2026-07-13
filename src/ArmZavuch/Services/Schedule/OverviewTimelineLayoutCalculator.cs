using ArmZavuch.Models;

namespace ArmZavuch.Services.Schedule;

/// <summary>Оценка высоты ячейки сводки по объёму текста (единая высота на весь «уровень» урока).</summary>
public static class OverviewTimelineLayoutCalculator
{
    public const int TimeBandHeight = 14;
    public const int MinCellHeight = 58;
    private const int CharsPerLineSingle = 15;
    private const int CharsPerLineSplit = 14;

    public static int EstimateContentHeight(OverviewTimelineCell cell)
    {
        if (cell.IsBreakColumn && cell.Parts.Count == 0)
            return 30;

        if (cell.Parts.Count == 0)
            return MinCellHeight;

        var body = cell.Parts.Count == 1
            ? EstimateSinglePartBlock(cell.Parts[0])
            : cell.Parts.Sum(EstimateSplitPartBlock) + (cell.Parts.Count - 1);

        return Math.Max(MinCellHeight, TimeBandHeight + body + 8);
    }

    private static int EstimateSinglePartBlock(OverviewTimelinePart part)
    {
        var subjectLines = EstimateLineCount(part.SubjectLine, CharsPerLineSingle);
        var teacherLines = EstimateLineCount(part.TeacherLine, CharsPerLineSingle);
        var roomLines = EstimateLineCount(part.RoomLine, CharsPerLineSingle);
        return 6 + subjectLines * 13 + teacherLines * 11 + roomLines * 10;
    }

    private static int EstimateSplitPartBlock(OverviewTimelinePart part)
    {
        var label = string.IsNullOrWhiteSpace(part.SubgroupLabel) ? 0 : 11;
        var subjectLines = EstimateLineCount(part.SubjectLine, CharsPerLineSplit);
        var teacherLines = EstimateLineCount(part.TeacherLine, CharsPerLineSplit);
        var roomLines = EstimateLineCount(part.RoomLine, CharsPerLineSplit);
        return label + subjectLines * 12 + teacherLines * 10 + Math.Max(roomLines, 0) * 10 + 4;
    }

    private static int EstimateLineCount(string text, int charsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerLine));
    }
}
