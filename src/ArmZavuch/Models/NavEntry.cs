namespace ArmZavuch.Models;

/// <summary>Пункт бокового меню главного окна.</summary>
public sealed class NavEntry
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Icon { get; init; }
    public string? Hint { get; init; }
}
