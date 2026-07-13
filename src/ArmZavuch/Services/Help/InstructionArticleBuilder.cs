namespace ArmZavuch.Services.Help;

/// <summary>Короткие помощники для сборки статей справки без дублирования разметки.</summary>
internal static class InstructionArticleBuilder
{
    public static InstructionHeadingBlock H(string text) => new() { Text = text };
    public static InstructionParagraphBlock P(string text) => new() { Text = text };
    public static InstructionBulletBlock B(params string[] items) => new() { Items = items };
    public static InstructionStepsBlock S(params string[] steps) => new() { Steps = steps };
    public static InstructionImportantBlock Important(string text) => new() { Text = text };

    public static InstructionImportantBlock Warn(string text) => Important(text);

    public static InstructionParagraphBlock Tip(string text) => P(text);

    public static InstructionUiSampleBlock Ui(InstructionUiSampleKind kind, string? text = null, string? caption = null) =>
        new() { Kind = kind, Text = text, Caption = caption };

    public static InstructionUiGalleryBlock Gallery(params InstructionUiSampleBlock[] samples) =>
        new() { Samples = samples };

    public static InstructionArticle Group(string id, string title, int sort, string? parent = null) =>
        new()
        {
            Id = id,
            ParentId = parent,
            Title = title,
            SortOrder = sort,
            IsGroup = true
        };

    public static InstructionArticle Article(
        string id,
        string title,
        int sort,
        InstructionBlock[] blocks,
        string? parent = null,
        string? moduleKey = null,
        string[]? related = null,
        string[]? keywords = null) =>
        new()
        {
            Id = id,
            ParentId = parent,
            Title = title,
            SortOrder = sort,
            Blocks = blocks,
            ModuleKey = moduleKey,
            RelatedIds = related ?? [],
            Keywords = keywords ?? []
        };
}
