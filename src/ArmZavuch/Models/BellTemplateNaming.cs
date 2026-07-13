namespace ArmZavuch.Models;

/// <summary>Русские названия шаблонов звонков. Вход: имя в БД. Выход: подпись для UI.</summary>
public static class BellTemplateNaming
{
    public const string Grade1 = "1 класс";
    public const string Grade1SecondHalf = "1 класс 2 полугодие";
    public const string Primary = "Начальная (2–5)";
    public const string Standard = "Стандарт (5–11)";
    public const string SecondShift = "2 смена (5–11)";

    public static string ToDisplay(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var trimmed = name.Trim();
        if (trimmed.StartsWith("Legacy · ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["Legacy · ".Length..].Trim();

        return trimmed switch
        {
            "Начальная (2–4)" => Primary,
            "Стандарт" => Standard,
            _ => trimmed
        };
    }

    public static string FormatImportedShiftName(int shift, string firstStart, string lastEnd) =>
        shift == 2
            ? $"2 смена · {firstStart}–{lastEnd}"
            : $"1 смена · {firstStart}–{lastEnd}";
}
