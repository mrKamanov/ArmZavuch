namespace ArmZavuch.Models;

public sealed class BuildingDeleteResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static BuildingDeleteResult Ok() => new() { Success = true };

    public static BuildingDeleteResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
