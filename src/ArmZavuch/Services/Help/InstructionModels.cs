namespace ArmZavuch.Services.Help;

/// <summary>Блок текста статьи справки: абзац, список, шаги, подсказка.</summary>
public abstract class InstructionBlock;

public sealed class InstructionHeadingBlock : InstructionBlock
{
    public required string Text { get; init; }
}

public sealed class InstructionParagraphBlock : InstructionBlock
{
    public required string Text { get; init; }
}

public sealed class InstructionBulletBlock : InstructionBlock
{
    public required IReadOnlyList<string> Items { get; init; }
}

public sealed class InstructionStepsBlock : InstructionBlock
{
    public required IReadOnlyList<string> Steps { get; init; }
}

public sealed class InstructionTipBlock : InstructionBlock
{
    public required string Text { get; init; }
}

public sealed class InstructionImportantBlock : InstructionBlock
{
    public required string Text { get; init; }
}

/// <summary>Наглядный образец кнопки, индикатора или ячейки как в программе.</summary>
public sealed class InstructionUiSampleBlock : InstructionBlock
{
    public required InstructionUiSampleKind Kind { get; init; }
    public string? Text { get; init; }
    public string? Caption { get; init; }
}

/// <summary>Несколько образцов в одной строке (легенда цветов, кнопки).</summary>
public sealed class InstructionUiGalleryBlock : InstructionBlock
{
    public required IReadOnlyList<InstructionUiSampleBlock> Samples { get; init; }
}

/// <summary>Статья справки: заголовок, дерево, текст, ссылки на модули и смежные темы.</summary>
public sealed class InstructionArticle
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public required string Title { get; init; }
    public int SortOrder { get; init; }
    public bool IsGroup { get; init; }
    public string? ModuleKey { get; init; }
    public IReadOnlyList<InstructionBlock> Blocks { get; init; } = [];
    public IReadOnlyList<string> RelatedIds { get; init; } = [];
    public IReadOnlyList<string> Keywords { get; init; } = [];
}

/// <summary>Быстрый маршрут «мне срочно нужно…».</summary>
public sealed class InstructionQuickRoute
{
    public required string Title { get; init; }
    public required string Hint { get; init; }
    public required string ArticleId { get; init; }
}

/// <summary>Каталог справки: все статьи и быстрые маршруты.</summary>
public sealed class InstructionCatalog
{
    public InstructionCatalog(IReadOnlyList<InstructionArticle> articles, IReadOnlyList<InstructionQuickRoute> quickRoutes)
    {
        Articles = articles;
        QuickRoutes = quickRoutes;
        ById = articles.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<InstructionArticle> Articles { get; }
    public IReadOnlyList<InstructionQuickRoute> QuickRoutes { get; }
    public IReadOnlyDictionary<string, InstructionArticle> ById { get; }
}
