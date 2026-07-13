namespace ArmZavuch.Models;

/// <summary>Статусы отсутствия сотрудника (больничный, отгул и т.д.).</summary>
public static class StaffStatusTypes
{
    public const string Sick = "Sick";
    public const string Leave = "Leave";
    public const string Other = "Other";

    public static readonly string[] All = [Sick, Leave, Other];

    public static string ToDisplay(string type) => type switch
    {
        Sick => "Больничный",
        Leave => "Отгул",
        _ => "Другое"
    };

    public static bool BlocksWork(string type) => type is Sick or Leave or Other;

    /// <summary>Иконка причины отсутствия для монитора и таблицы замен.</summary>
    public static string ToIcon(string? absenceNote)
    {
        if (string.IsNullOrWhiteSpace(absenceNote))
            return "";

        var lower = absenceNote.Trim().ToLowerInvariant();
        if (lower.StartsWith("больнич"))
            return "🤒 ";
        if (lower.StartsWith("отгул"))
            return "📋 ";
        return "⚠️ ";
    }

    /// <summary>Краткая подпись: «Больничный» или «Больничный: номер листа».</summary>
    public static string ResolveShortLabel(string? absenceNote)
    {
        if (string.IsNullOrWhiteSpace(absenceNote))
            return "";

        var trimmed = absenceNote.Trim();
        foreach (var type in All)
        {
            var display = ToDisplay(type);
            if (!trimmed.StartsWith(display, StringComparison.OrdinalIgnoreCase))
                continue;

            var tail = trimmed[display.Length..].TrimStart(':', ' ', '—', '-').Trim();
            return string.IsNullOrEmpty(tail) ? display : $"{display}: {tail}";
        }

        return trimmed;
    }

    /// <summary>Префикс для строки «нужна замена»: «🤒 Больничный · ».</summary>
    public static string FormatPendingPrefix(string? absenceNote)
    {
        var label = ResolveShortLabel(absenceNote);
        return string.IsNullOrEmpty(label) ? "" : $"{ToIcon(absenceNote)}{label} · ";
    }

    public static string FormatPendingStatus(string? absenceNote, string detail = "нужна замена") =>
        $"{FormatPendingPrefix(absenceNote)}{detail}";
}
