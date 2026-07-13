using System.Windows;
using System.Windows.Media.Imaging;
using ArmZavuch.Models;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace ArmZavuch.Services.Export;

/// <summary>Экспорт листа замен в Excel, PNG и буфер обмена (ТЗ §7.2, §7.3).</summary>
public sealed class SubstitutionExportService
{
    public static List<SubstitutionLine> BuildLines(IEnumerable<LessonSlot> lessons)
    {
        var lines = lessons
            .Where(l => l.ReplacementTeacherName is not null || l.IsCancelled)
            .Select(l => new SubstitutionLine
            {
                LessonNumber = l.LessonNumber,
                DisplayLessonLabel = l.DisplayLessonLabel,
                Time = l.TimeDisplay,
                ClassName = l.ClassName,
                SubjectName = l.SubjectName,
                OriginalTeacher = l.TeacherName,
                ReplacementTeacher = l.IsCancelled
                    ? "отменён"
                    : l.ReplacementTeacherName ?? "",
                RoomNumber = l.RoomNumber,
                BuildingName = l.BuildingName,
                ClassShift = l.ClassShift,
                IsPending = l.ReplacementTeacherName?.Contains("замена", StringComparison.OrdinalIgnoreCase) == true,
                IsCancelled = l.IsCancelled
            })
            .ToList();

        return SubstitutionExportDisplayBuilder.SortLines(lines).ToList();
    }

    public void ExportExcel(DateOnly date, string schoolName, List<SubstitutionLine> lines)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"Замены_{date:yyyy-MM-dd}.xlsx"
        };
        if (dlg.ShowDialog() != true)
            return;

        var document = CreateDocument(date, schoolName, lines);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Замены");
        ws.Cell(1, 1).Value = document.SchoolName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value =
            $"Лист замен · {document.Date.ToString("dddd, d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))}";
        ws.Cell(3, 1).Value = FormatSummaryLine(document);

        var headerRow = 5;
        var headers = new[] { "Смена", "Урок", "Время", "Класс", "Предмет", "Было", "Стало", "Кабинет", "Здание" };
        for (var col = 0; col < headers.Length; col++)
            ws.Cell(headerRow, col + 1).Value = headers[col];

        ws.Range(headerRow, 1, headerRow, headers.Length).Style.Font.Bold = true;
        ws.Range(headerRow, 1, headerRow, headers.Length).Style.Fill.BackgroundColor =
            XLColor.FromHtml("#2563EB");
        ws.Range(headerRow, 1, headerRow, headers.Length).Style.Font.FontColor = XLColor.White;

        var row = headerRow + 1;
        foreach (var line in document.Lines)
        {
            ws.Cell(row, 1).Value = line.ClassShift;
            ws.Cell(row, 2).Value = line.LessonTitle;
            ws.Cell(row, 3).Value = line.Time;
            ws.Cell(row, 4).Value = line.ClassName;
            ws.Cell(row, 5).Value = line.SubjectName;
            ws.Cell(row, 6).Value = line.OriginalTeacher;
            ws.Cell(row, 7).Value = line.ExportKind == SubstitutionExportKind.Cancelled
                ? "ОТМЕНЁН"
                : line.ReplacementTeacher;
            ws.Cell(row, 8).Value = line.RoomNumber;
            ws.Cell(row, 9).Value = line.BuildingName;

            var fill = line.ExportKind switch
            {
                SubstitutionExportKind.Cancelled => XLColor.FromHtml("#FEE2E2"),
                SubstitutionExportKind.Pending => XLColor.FromHtml("#FFEDD5"),
                _ => XLColor.FromHtml("#F0FDF4")
            };
            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = fill;
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(dlg.FileName);
    }

    public int CopyToClipboard(DateOnly date, string schoolName, List<SubstitutionLine> lines)
    {
        var document = CreateDocument(date, schoolName, lines);
        var pages = SubstitutionExportImageRenderer.RenderPages(document);
        var text = SubstitutionExportPlainTextFormatter.Format(document, pages.Count);

        var data = new DataObject();
        data.SetImage(pages[0]);
        data.SetText(text);
        Clipboard.SetDataObject(data, true);
        return pages.Count;
    }

    public int SaveImage(DateOnly date, string schoolName, List<SubstitutionLine> lines)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            FileName = $"Замены_{date:yyyy-MM-dd}.png"
        };
        if (dlg.ShowDialog() != true)
            return 0;

        var document = CreateDocument(date, schoolName, lines);
        var pages = SubstitutionExportImageRenderer.RenderPages(document);
        var directory = Path.GetDirectoryName(dlg.FileName) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(dlg.FileName);
        var extension = Path.GetExtension(dlg.FileName);

        for (var index = 0; index < pages.Count; index++)
        {
            var path = pages.Count == 1
                ? dlg.FileName
                : Path.Combine(directory, $"{baseName}_{index + 1}{extension}");

            SaveBitmap(pages[index], path);
        }

        return pages.Count;
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
    }

    private static SubstitutionExportDocument CreateDocument(
        DateOnly date,
        string schoolName,
        IReadOnlyList<SubstitutionLine> lines) =>
        new()
        {
            SchoolName = schoolName,
            Date = date,
            Lines = lines
        };

    private static string FormatSummaryLine(SubstitutionExportDocument document)
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
}
