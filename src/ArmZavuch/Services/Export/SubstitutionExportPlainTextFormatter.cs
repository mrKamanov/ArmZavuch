using System.Globalization;
using System.Text;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Текстовое представление листа замен для буфера обмена.</summary>
public static class SubstitutionExportPlainTextFormatter
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Format(SubstitutionExportDocument document, int pageCount = 1)
    {
        var builder = new StringBuilder();
        builder.AppendLine(document.SchoolName);
        var dateLine = $"Лист замен · {FormatDate(document.Date)}";
        if (pageCount > 1)
            dateLine += $" · {pageCount} стр. в PNG";
        builder.AppendLine(dateLine);
        builder.AppendLine(FormatSummary(document));
        builder.AppendLine(new string('─', 42));

        if (document.Lines.Count == 0)
        {
            builder.AppendLine("Замен нет.");
            return builder.ToString().TrimEnd();
        }

        foreach (var shiftGroup in document.Lines.GroupBy(line => line.ClassShift).OrderBy(g => g.Key))
        {
            builder.AppendLine();
            builder.AppendLine($"{shiftGroup.Key} смена");

            foreach (var lessonGroup in shiftGroup.GroupBy(line => line.LessonNumber).OrderBy(g => g.Key))
            {
                var first = lessonGroup.First();
                var timePart = string.IsNullOrWhiteSpace(first.Time) ? "" : $" ({first.Time})";
                builder.AppendLine($"{first.LessonTitle}{timePart}");

                foreach (var line in lessonGroup.OrderBy(l => l.ClassName, StringComparer.CurrentCultureIgnoreCase))
                    AppendLineBlock(builder, line);
            }
        }

        builder.AppendLine();
        if (pageCount > 1)
            builder.AppendLine("В буфере — страница 1 как картинка. Все страницы: «PNG».");
        builder.AppendLine($"Сформировано {DateTime.Now:dd.MM.yyyy HH:mm}");
        return builder.ToString().TrimEnd();
    }

    private static void AppendLineBlock(StringBuilder builder, SubstitutionLine line)
    {
        builder.AppendLine($"  {line.ClassName} · {line.SubjectName}");
        builder.AppendLine($"  Было: {line.OriginalTeacher}");

        var after = line.ExportKind switch
        {
            SubstitutionExportKind.Cancelled => "ОТМЕНЁН",
            SubstitutionExportKind.Pending => line.ReplacementTeacher,
            _ => line.ReplacementTeacher
        };
        builder.AppendLine($"  Стало: {after}");

        if (!string.IsNullOrWhiteSpace(line.RoomDisplay))
            builder.AppendLine($"  {line.RoomDisplay}");
    }

    private static string FormatSummary(SubstitutionExportDocument document)
    {
        if (document.Lines.Count == 0)
            return "Замен нет";

        var parts = new List<string>();
        if (document.AssignedCount > 0)
            parts.Add($"{document.AssignedCount} назначено");
        if (document.PendingCount > 0)
            parts.Add($"{document.PendingCount} ожидает");
        if (document.CancelledCount > 0)
            parts.Add($"{document.CancelledCount} отменено");

        return string.Join(" · ", parts);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dddd, d MMMM yyyy", Russian);
}
