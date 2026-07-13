namespace ArmZavuch.Services.Help;

/// <summary>Собирает каталог справки и ищет статьи по запросу.</summary>
public static class InstructionWiki
{
    public static InstructionCatalog Catalog { get; } = Build();

    public static IReadOnlyList<InstructionArticle> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var q = query.Trim();
        return Catalog.Articles
            .Where(a => !a.IsGroup && Matches(a, q))
            .OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static InstructionCatalog Build()
    {
        var articles = new List<InstructionArticle>();
        articles.AddRange(InstructionArticlesRoutes.Groups);
        articles.AddRange(InstructionArticlesRoutes.All);
        articles.AddRange(InstructionArticlesStart.All);
        articles.AddRange(InstructionArticlesGlossary.All);
        articles.AddRange(InstructionArticlesDirectories.All);
        articles.AddRange(InstructionArticlesConstructor.All);
        articles.AddRange(InstructionArticlesDispatcher.All);
        articles.AddRange(InstructionArticlesDayOverview.All);
        articles.AddRange(InstructionArticlesSupport.All);
        return new InstructionCatalog(articles, InstructionArticlesRoutes.QuickRoutes);
    }

    private static bool Matches(InstructionArticle article, string query)
    {
        if (article.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            return true;

        foreach (var keyword in article.Keywords)
        {
            if (keyword.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }

        foreach (var block in article.Blocks)
        {
            if (block is InstructionParagraphBlock p
                && p.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                return true;
            if (block is InstructionHeadingBlock h
                && h.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }

        return false;
    }
}
