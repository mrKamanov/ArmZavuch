namespace ArmZavuch.Models;

/// <summary>Справочник зданий (ТЗ §3).</summary>
public sealed class Building : SelectableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#2563EB";
}
