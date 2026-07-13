using ArmZavuch.Models;

namespace ArmZavuch.Data;

/// <summary>Встроенные шаблоны звонков и нагрузки, сохраняемые при полной очистке.</summary>
public static class BuiltInDataCatalog
{
    public static IReadOnlyList<string> BellTemplateNames { get; } =
    [
        BellTemplateNaming.Grade1,
        BellTemplateNaming.Grade1SecondHalf,
        BellTemplateNaming.Primary,
        "Начальная (2–4)",
        BellTemplateNaming.Standard,
        "Стандарт",
        BellTemplateNaming.SecondShift
    ];

    public static string SqlBellNamesInList() =>
        string.Join(", ", BellTemplateNames.Select(EscapeSqlLiteral));

    private static string EscapeSqlLiteral(string value) => $"'{value.Replace("'", "''")}'";
}
