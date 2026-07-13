using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Строка или заголовок смены в листе замен для PNG.</summary>
public sealed class SubstitutionExportDisplayItem
{
    public bool IsShiftHeader { get; init; }
    public string? ShiftTitle { get; init; }
    public SubstitutionLine? Line { get; init; }
    public int Height { get; init; }

    public static SubstitutionExportDisplayItem ShiftHeader(int shift) => new()
    {
        IsShiftHeader = true,
        ShiftTitle = $"{shift} смена",
        Height = 32
    };

    public static SubstitutionExportDisplayItem Data(SubstitutionLine line, int height) => new()
    {
        Line = line,
        Height = height
    };
}

/// <summary>Сортировка замен и вставка заголовков смен.</summary>
public static class SubstitutionExportDisplayBuilder
{
    public static IReadOnlyList<SubstitutionLine> SortLines(IEnumerable<SubstitutionLine> lines) =>
        lines
            .OrderBy(line => line.ClassShift)
            .ThenBy(line => line.LessonNumber)
            .ThenBy(line => line.ClassName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static IReadOnlyList<SubstitutionExportDisplayItem> Build(IReadOnlyList<SubstitutionLine> lines)
    {
        var items = new List<SubstitutionExportDisplayItem>();
        int? lastShift = null;

        foreach (var line in lines)
        {
            if (lastShift != line.ClassShift)
            {
                items.Add(SubstitutionExportDisplayItem.ShiftHeader(line.ClassShift));
                lastShift = line.ClassShift;
            }

            items.Add(SubstitutionExportDisplayItem.Data(
                line,
                SubstitutionExportImageRenderer.EstimateRowHeight(line)));
        }

        return items;
    }
}
