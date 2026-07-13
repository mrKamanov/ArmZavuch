namespace ArmZavuch.Models;

/// <summary>Тип кабинета для справочника и подбора спортзала.</summary>
public static class RoomKinds
{
    public const string RegularStorage = "";
    public const string SportHall = "Спортзал";

    public static readonly string[] Options =
    [
        "Обычный",
        SportHall,
        "Лаборатория",
        "Мастерская",
        "Компьютерный класс",
        "Актовый зал"
    ];

    public static string ToDisplay(string? storage)
    {
        if (string.IsNullOrWhiteSpace(storage)
            || storage.Equals("Regular", StringComparison.OrdinalIgnoreCase))
            return "Обычный";

        return storage.Trim();
    }

    public static string FromDisplay(string? display)
    {
        if (string.IsNullOrWhiteSpace(display)
            || display.Equals("Обычный", StringComparison.OrdinalIgnoreCase)
            || display.Equals("Regular", StringComparison.OrdinalIgnoreCase))
            return RegularStorage;

        return display.Trim();
    }
}
