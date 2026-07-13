namespace ArmZavuch.Models;

public sealed class DirectoryClearOption
{
    public required DirectoryClearSection Section { get; init; }
    public required string TabName { get; init; }
    public required string Hint { get; init; }
}
