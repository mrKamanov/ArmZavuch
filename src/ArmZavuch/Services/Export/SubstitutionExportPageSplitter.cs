using ArmZavuch.Models;

namespace ArmZavuch.Services.Export;

/// <summary>Одна страница PNG листа замен.</summary>
public sealed class SubstitutionExportPage
{
    public required int PageIndex { get; init; }
    public required int TotalPages { get; init; }
    public required bool IsFirstPage { get; init; }
    public required IReadOnlyList<SubstitutionExportDisplayItem> Items { get; init; }
    public required int GlobalDataRowOffset { get; init; }
}

/// <summary>Делит строки замен на страницы по максимальной высоте PNG.</summary>
public static class SubstitutionExportPageSplitter
{
    public const int MaxPageHeight = 1280;
    private const int Padding = 24;
    private const int HeaderBlockHeight = 92;
    private const int SummaryHeight = 34;
    private const int LegendHeight = 28;
    private const int TableHeaderHeight = 36;
    private const int FooterHeight = 28;
    private const int ContinuationHeaderHeight = 44;
    private const int EmptyRowHeight = 44;

    public static IReadOnlyList<SubstitutionExportPage> Split(SubstitutionExportDocument document)
    {
        if (document.Lines.Count == 0)
        {
            return
            [
                new SubstitutionExportPage
                {
                    PageIndex = 1,
                    TotalPages = 1,
                    IsFirstPage = true,
                    Items = [],
                    GlobalDataRowOffset = 0
                }
            ];
        }

        var items = SubstitutionExportDisplayBuilder.Build(document.Lines);
        var pages = new List<SubstitutionExportPage>();
        var startIndex = 0;
        var dataRowsBeforePage = 0;

        while (startIndex < items.Count)
        {
            var isFirst = pages.Count == 0;
            var overhead = isFirst
                ? Padding * 2 + HeaderBlockHeight + SummaryHeight + LegendHeight + TableHeaderHeight + FooterHeight
                : Padding * 2 + ContinuationHeaderHeight + TableHeaderHeight + FooterHeight;

            var budget = MaxPageHeight - overhead;
            var used = 0;
            var count = 0;

            while (startIndex + count < items.Count)
            {
                var height = items[startIndex + count].Height;
                if (used + height > budget && count > 0)
                    break;

                used += height;
                count++;
            }

            if (count == 0)
                count = 1;

            var pageItems = items.Skip(startIndex).Take(count).ToList();
            pages.Add(new SubstitutionExportPage
            {
                PageIndex = pages.Count + 1,
                TotalPages = 0,
                IsFirstPage = isFirst,
                Items = pageItems,
                GlobalDataRowOffset = dataRowsBeforePage
            });

            dataRowsBeforePage += pageItems.Count(item => !item.IsShiftHeader);
            startIndex += count;
        }

        var totalPages = pages.Count;
        return pages
            .Select(page => new SubstitutionExportPage
            {
                PageIndex = page.PageIndex,
                TotalPages = totalPages,
                IsFirstPage = page.IsFirstPage,
                Items = page.Items,
                GlobalDataRowOffset = page.GlobalDataRowOffset
            })
            .ToList();
    }
}
