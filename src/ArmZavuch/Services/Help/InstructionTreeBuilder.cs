namespace ArmZavuch.Services.Help;

/// <summary>Узел дерева оглавления справки для TreeView.</summary>
public sealed class InstructionTopicNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public InstructionArticle? Article { get; init; }
    public IList<InstructionTopicNode> Children { get; } = [];
}

/// <summary>Строит дерево разделов из плоского каталога статей.</summary>
public static class InstructionTreeBuilder
{
    public static IReadOnlyList<InstructionTopicNode> Build(InstructionCatalog catalog)
    {
        var roots = catalog.Articles
            .Where(a => a.IsGroup)
            .OrderBy(a => a.SortOrder);

        var list = new List<InstructionTopicNode>();
        foreach (var group in roots)
        {
            var node = new InstructionTopicNode { Id = group.Id, Title = group.Title };
            foreach (var child in catalog.Articles
                         .Where(a => !a.IsGroup && string.Equals(a.ParentId, group.Id, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(a => a.SortOrder))
            {
                node.Children.Add(new InstructionTopicNode
                {
                    Id = child.Id,
                    Title = child.Title,
                    Article = child
                });
            }

            list.Add(node);
        }

        return list;
    }
}
