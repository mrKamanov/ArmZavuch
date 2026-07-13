namespace ArmZavuch.Models;

/// <summary>Готовая палитра цветов для зданий — без HEX-кодов для пользователя.</summary>
public static class BuildingColors
{
    public sealed record Choice(string Label, string Hex);

    public static IReadOnlyList<Choice> Palette { get; } =
    [
        new("Синий", "#2563EB"),
        new("Красный", "#DC2626"),
        new("Зелёный", "#16A34A"),
        new("Фиолетовый", "#9333EA"),
        new("Оранжевый", "#EA580C"),
        new("Бирюзовый", "#0891B2"),
        new("Жёлтый", "#CA8A04"),
        new("Серый", "#64748B"),
        new("Розовый", "#DB2777"),
        new("Изумрудный", "#059669"),
        new("Индиго", "#4F46E5"),
        new("Коричневый", "#78716C")
    ];

    public static string DefaultHex => Palette[0].Hex;

    public static string Normalize(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return DefaultHex;
        var value = hex.Trim();
        if (!value.StartsWith('#'))
            value = "#" + value;
        return value.Length is 7 or 9 ? value.ToUpperInvariant() : DefaultHex;
    }

    /// <summary>Следующий свободный цвет из палитры (чтобы новые здания не сливались).</summary>
    public static string SuggestNext(IEnumerable<Building> existing)
    {
        var used = existing.Select(b => Normalize(b.ColorHex).ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var choice in Palette)
        {
            if (!used.Contains(Normalize(choice.Hex)))
                return choice.Hex;
        }
        return Palette[existing.Count() % Palette.Count].Hex;
    }

    public static IReadOnlyDictionary<string, string> ToColorMap(IEnumerable<Building> buildings) =>
        buildings
            .GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Normalize(g.First().ColorHex), StringComparer.OrdinalIgnoreCase);

    public static string ResolveHex(string? buildingName, IReadOnlyDictionary<string, string>? colors)
    {
        if (string.IsNullOrWhiteSpace(buildingName) || colors is null)
            return "#94A3B8";
        return colors.TryGetValue(buildingName, out var hex) ? Normalize(hex) : "#94A3B8";
    }

    public static bool IsTaken(string hex, IEnumerable<Building> buildings, int? exceptBuildingId = null)
    {
        var normalized = Normalize(hex);
        return buildings.Any(b =>
            (exceptBuildingId is null || b.Id != exceptBuildingId)
            && Normalize(b.ColorHex).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string LabelFor(string? hex) =>
        Palette.FirstOrDefault(c => Normalize(c.Hex).Equals(Normalize(hex), StringComparison.OrdinalIgnoreCase))?.Label
        ?? Normalize(hex);
}
