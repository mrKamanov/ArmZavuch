using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Рисует лист замен как PNG: таблица «было → стало», с постраничной разбивкой.</summary>
public static class SubstitutionExportImageRenderer
{
    private const int Width = 720;
    private const int Padding = 24;
    private const int HeaderBlockHeight = 92;
    private const int SummaryHeight = 34;
    private const int LegendHeight = 28;
    private const int ContinuationHeaderHeight = 44;
    private const int TableHeaderHeight = 36;
    private const int FooterHeight = 28;
    private const int EmptyRowHeight = 44;
    private const int MinRowHeight = 58;

    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly Typeface Typeface = new("Segoe UI");

    private static readonly Color Primary = Color.FromRgb(37, 99, 235);
    private static readonly Color Text = Color.FromRgb(15, 23, 42);
    private static readonly Color Muted = Color.FromRgb(100, 116, 139);
    private static readonly Color Border = Color.FromRgb(226, 232, 240);
    private static readonly Color RowAlt = Color.FromRgb(248, 250, 252);
    private static readonly Color Assigned = Color.FromRgb(22, 163, 74);
    private static readonly Color Pending = Color.FromRgb(180, 83, 9);
    private static readonly Color Cancelled = Color.FromRgb(220, 38, 38);

    private static readonly int[] ColumnWidths = [86, 58, 124, 138, 26, 138, 102];

    public static IReadOnlyList<BitmapSource> RenderPages(SubstitutionExportDocument document)
    {
        var pages = SubstitutionExportPageSplitter.Split(document);
        return pages.Select(page => RenderPage(document, page)).ToList();
    }

    public static int EstimateRowHeight(SubstitutionLine line)
    {
        var texts = new[]
        {
            line.LessonTitle + "\n" + line.Time,
            line.ClassName,
            line.SubjectName,
            line.OriginalTeacher,
            line.ReplacementTeacher,
            line.RoomNumber
        };

        var maxHeight = texts
            .Select(text => CreateFormattedText(text, 11, FontWeights.Normal, Text, ColumnWidths[2]).Height)
            .DefaultIfEmpty(0)
            .Max();

        return (int)Math.Ceiling(Math.Max(MinRowHeight, maxHeight + 22));
    }

    private static BitmapSource RenderPage(SubstitutionExportDocument document, SubstitutionExportPage page)
    {
        var contentWidth = Width - Padding * 2;
        var itemsHeight = page.Items.Count == 0 ? EmptyRowHeight : page.Items.Sum(item => item.Height);
        var fixedHeight = page.IsFirstPage
            ? Padding * 2 + HeaderBlockHeight + SummaryHeight + LegendHeight + TableHeaderHeight + FooterHeight
            : Padding * 2 + ContinuationHeaderHeight + TableHeaderHeight + FooterHeight;
        var height = fixedHeight + itemsHeight;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, height));
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                new Pen(new SolidColorBrush(Border), 1),
                new Rect(12, 12, Width - 24, height - 24),
                12, 12);

            var y = Padding;
            if (page.IsFirstPage)
            {
                y = DrawHeader(dc, document, y, contentWidth, page);
                y = DrawSummary(dc, document, y, contentWidth);
                y = DrawLegend(dc, y, contentWidth);
            }
            else
            {
                y = DrawContinuationHeader(dc, document, y, contentWidth, page);
            }

            y = DrawTableHeader(dc, y, contentWidth);

            if (page.Items.Count == 0)
            {
                DrawRowBackground(dc, y, contentWidth, EmptyRowHeight, SubstitutionExportKind.Assigned, false);
                DrawText(dc, "Замен нет", 13, FontWeights.Normal, Muted, Padding, y + 12, contentWidth);
            }
            else
            {
                var dataRowIndex = page.GlobalDataRowOffset;
                foreach (var item in page.Items)
                {
                    if (item.IsShiftHeader)
                    {
                        DrawShiftHeader(dc, y, contentWidth, item.ShiftTitle ?? "");
                        y += item.Height;
                        continue;
                    }

                    var line = item.Line!;
                    var alternate = dataRowIndex % 2 == 1;
                    DrawRowBackground(dc, y, contentWidth, item.Height, line.ExportKind, alternate);
                    DrawRow(dc, line, y, contentWidth);
                    dataRowIndex++;
                    y += item.Height;
                }
            }

            var footer = page.TotalPages > 1
                ? $"Стр. {page.PageIndex}/{page.TotalPages} · сформировано {DateTime.Now:dd.MM.yyyy HH:mm}"
                : $"Сформировано {DateTime.Now:dd.MM.yyyy HH:mm}";

            DrawText(
                dc,
                footer,
                10,
                FontWeights.Normal,
                Muted,
                Padding,
                height - Padding - 18,
                contentWidth,
                TextAlignment.Right);
        }

        var bitmap = new RenderTargetBitmap(Width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void DrawShiftHeader(DrawingContext dc, int y, double contentWidth, string title)
    {
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            null,
            new Rect(Padding, y, contentWidth, 32));
        DrawText(dc, title, 12, FontWeights.SemiBold, Text, Padding + 8, y + 8, contentWidth);
    }

    private static int DrawHeader(
        DrawingContext dc,
        SubstitutionExportDocument document,
        int y,
        double contentWidth,
        SubstitutionExportPage page)
    {
        DrawText(dc, document.SchoolName, 18, FontWeights.SemiBold, Text, Padding, y, contentWidth);
        var dateText = $"Лист замен · {document.Date.ToString("dddd, d MMMM yyyy", Russian)}";
        if (page.TotalPages > 1)
            dateText += $" · стр. {page.PageIndex}/{page.TotalPages}";
        DrawText(dc, dateText, 13, FontWeights.Normal, Muted, Padding, y + 28, contentWidth);
        return y + HeaderBlockHeight;
    }

    private static int DrawContinuationHeader(
        DrawingContext dc,
        SubstitutionExportDocument document,
        int y,
        double contentWidth,
        SubstitutionExportPage page)
    {
        DrawText(
            dc,
            $"{document.SchoolName} · замены · стр. {page.PageIndex}/{page.TotalPages}",
            13,
            FontWeights.SemiBold,
            Text,
            Padding,
            y + 8,
            contentWidth);
        DrawText(
            dc,
            document.Date.ToString("dddd, d MMMM yyyy", Russian),
            11,
            FontWeights.Normal,
            Muted,
            Padding,
            y + 26,
            contentWidth);
        return y + ContinuationHeaderHeight;
    }

    private static int DrawSummary(DrawingContext dc, SubstitutionExportDocument document, int y, double contentWidth)
    {
        var x = (double)Padding;
        if (document.Lines.Count == 0)
        {
            DrawBadge(dc, ref x, y, "Замен нет", Muted, Color.FromRgb(241, 245, 249));
            return y + SummaryHeight;
        }

        if (document.AssignedCount > 0)
            DrawBadge(dc, ref x, y, $"{document.AssignedCount} назначено", Assigned, Color.FromRgb(220, 252, 231));
        if (document.PendingCount > 0)
            DrawBadge(dc, ref x, y, $"{document.PendingCount} ожидает", Pending, Color.FromRgb(255, 247, 237));
        if (document.CancelledCount > 0)
            DrawBadge(dc, ref x, y, $"{document.CancelledCount} отменено", Cancelled, Color.FromRgb(254, 226, 226));

        return y + SummaryHeight;
    }

    private static int DrawLegend(DrawingContext dc, int y, double contentWidth)
    {
        var legend = "■ назначено   ■ ожидает замену   ■ отменено";
        DrawText(dc, legend, 10, FontWeights.Normal, Muted, Padding, y + 6, contentWidth);
        return y + LegendHeight;
    }

    private static int DrawTableHeader(DrawingContext dc, int y, double contentWidth)
    {
        dc.DrawRectangle(new SolidColorBrush(Primary), null, new Rect(Padding, y, contentWidth, TableHeaderHeight));
        var headers = new[] { "Урок", "Класс", "Предмет", "Было", "", "Стало", "Каб." };
        var x = (double)Padding;
        for (var i = 0; i < headers.Length; i++)
        {
            if (!string.IsNullOrEmpty(headers[i]))
            {
                DrawText(
                    dc,
                    headers[i],
                    11,
                    FontWeights.SemiBold,
                    Colors.White,
                    x + 8,
                    y + 10,
                    ColumnWidths[i] - 12);
            }

            x += ColumnWidths[i];
        }

        return y + TableHeaderHeight;
    }

    private static void DrawRowBackground(
        DrawingContext dc,
        int y,
        double contentWidth,
        int rowHeight,
        SubstitutionExportKind kind,
        bool alternate)
    {
        var fill = alternate ? RowAlt : Colors.White;
        dc.DrawRectangle(new SolidColorBrush(fill), null, new Rect(Padding, y, contentWidth, rowHeight));

        var accent = kind switch
        {
            SubstitutionExportKind.Cancelled => Cancelled,
            SubstitutionExportKind.Pending => Pending,
            _ => Assigned
        };
        dc.DrawRectangle(new SolidColorBrush(accent), null, new Rect(Padding, y, 4, rowHeight));
        dc.DrawLine(
            new Pen(new SolidColorBrush(Border), 1),
            new Point(Padding, y + rowHeight),
            new Point(Padding + contentWidth, y + rowHeight));
    }

    private static void DrawRow(DrawingContext dc, SubstitutionLine line, int y, double contentWidth)
    {
        var lessonText = string.IsNullOrWhiteSpace(line.Time)
            ? line.LessonTitle
            : $"{line.LessonTitle}\n{line.Time}";

        var afterText = line.ExportKind switch
        {
            SubstitutionExportKind.Cancelled => "ОТМЕНЁН",
            SubstitutionExportKind.Pending => line.ReplacementTeacher,
            _ => line.ReplacementTeacher
        };

        var afterColor = line.ExportKind switch
        {
            SubstitutionExportKind.Cancelled => Cancelled,
            SubstitutionExportKind.Pending => Pending,
            _ => Text
        };

        var cells = new[]
        {
            (lessonText, Text, FontWeights.SemiBold),
            (line.ClassName, Text, FontWeights.SemiBold),
            (line.SubjectName, Text, FontWeights.SemiBold),
            (line.OriginalTeacher, Muted, FontWeights.Normal),
            ("→", Primary, FontWeights.Bold),
            (afterText, afterColor, FontWeights.SemiBold),
            (line.RoomNumber, Muted, FontWeights.Normal)
        };

        var x = (double)Padding;
        for (var i = 0; i < cells.Length; i++)
        {
            var (text, color, weight) = cells[i];
            DrawText(dc, text, 11, weight, color, x + 8, y + 10, ColumnWidths[i] - 12);
            x += ColumnWidths[i];
        }
    }

    private static void DrawBadge(DrawingContext dc, ref double x, int y, string text, Color textColor, Color bgColor)
    {
        const double badgeHeight = 24;
        const double paddingX = 10;
        var formatted = CreateFormattedText(text, 11, FontWeights.SemiBold, textColor, 200);
        var badgeWidth = formatted.Width + paddingX * 2;
        dc.DrawRoundedRectangle(
            new SolidColorBrush(bgColor),
            null,
            new Rect(x, y + 4, badgeWidth, badgeHeight),
            12, 12);
        dc.DrawText(formatted, new Point(x + paddingX, y + 8));
        x += badgeWidth + 8;
    }

    private static void DrawText(
        DrawingContext dc,
        string text,
        double size,
        FontWeight weight,
        Color color,
        double x,
        double y,
        double maxWidth,
        TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = CreateFormattedText(text, size, weight, color, maxWidth);
        formatted.TextAlignment = alignment;
        dc.DrawText(formatted, new Point(x, y));
    }

    private static FormattedText CreateFormattedText(
        string text,
        double size,
        FontWeight weight,
        Color color,
        double maxWidth)
    {
        var formatted = new FormattedText(
            text,
            Russian,
            FlowDirection.LeftToRight,
            Typeface,
            size,
            new SolidColorBrush(color),
            1.0)
        {
            MaxTextWidth = maxWidth,
            LineHeight = size * 1.25
        };
        return formatted;
    }
}
