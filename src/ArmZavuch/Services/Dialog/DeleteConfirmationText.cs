namespace ArmZavuch.Services.Dialog;

/// <summary>Заголовок и текст подтверждения удаления с правильным родом.</summary>
public static class DeleteConfirmationText
{
    public static (string Title, string Message) ForSingle(DeleteEntityKind kind, string label)
    {
        var name = label.Trim();
        return kind switch
        {
            DeleteEntityKind.Teacher => (
                "Удалить сотрудника?",
                $"«{name}» {PersonDeletedVerb(name)} из справочника без возможности отмены."),

            DeleteEntityKind.Subject => (
                "Удалить предмет?",
                $"«{name}» будет удалён из справочника без возможности отмены."),

            DeleteEntityKind.Building => (
                "Удалить здание?",
                $"«{name}» будет удалено из справочника без возможности отмены."),

            DeleteEntityKind.SchoolClass => (
                "Удалить класс?",
                $"«{name}» будет удалён вместе с нагрузкой, уроками в шаблонах недели и привязками педагогов."),

            DeleteEntityKind.Room => (
                "Удалить кабинет?",
                $"«{name}» будет удалён из справочника без возможности отмены."),

            DeleteEntityKind.Curriculum => (
                "Удалить запись нагрузки?",
                $"«{name}» будет удалена без возможности отмены."),

            DeleteEntityKind.Bell => (
                "Удалить запись звонков?",
                $"«{name}» будет удалена без возможности отмены."),

            DeleteEntityKind.Template => (
                "Удалить шаблон?",
                $"«{name}» будет удалён без возможности отмены."),

            DeleteEntityKind.Period => (
                "Удалить период?",
                $"«{name}» будет удалён без возможности отмены."),

            DeleteEntityKind.CalendarEntry => (
                "Удалить запись календаря?",
                $"«{name}» будет удалена без возможности отмены."),

            _ => (
                "Удалить запись?",
                $"«{name}» будет удалена без возможности отмены.")
        };
    }

    public static (string Title, string Message) ForMany(DeleteEntityKind kind, int count)
    {
        if (count <= 0)
            return ("Удалить записи?", "Нечего удалять.");

        return kind switch
        {
            DeleteEntityKind.Teacher => (
                $"Удалить {count} {Plural(count, "сотрудника", "сотрудников", "сотрудников")}?",
                $"Будут удалены {count} {Plural(count, "сотрудник", "сотрудника", "сотрудников")} без возможности отмены."),

            DeleteEntityKind.Subject => (
                $"Удалить {count} {Plural(count, "предмет", "предмета", "предметов")}?",
                $"Будут удалены {count} {Plural(count, "предмет", "предмета", "предметов")} без возможности отмены."),

            DeleteEntityKind.Building => (
                $"Удалить {count} {Plural(count, "здание", "здания", "зданий")}?",
                $"Будут удалены {count} {Plural(count, "здание", "здания", "зданий")} без возможности отмены."),

            DeleteEntityKind.SchoolClass => (
                $"Удалить {count} {Plural(count, "класс", "класса", "классов")}?",
                $"Будут удалены {count} {Plural(count, "класс", "класса", "классов")} вместе с нагрузкой и уроками в шаблонах недели."),

            DeleteEntityKind.Room => (
                $"Удалить {count} {Plural(count, "кабинет", "кабинета", "кабинетов")}?",
                $"Будут удалены {count} {Plural(count, "кабинет", "кабинета", "кабинетов")} без возможности отмены."),

            DeleteEntityKind.Curriculum => (
                $"Удалить {count} {Plural(count, "запись нагрузки", "записи нагрузки", "записей нагрузки")}?",
                $"Будут удалены {count} {Plural(count, "запись нагрузки", "записи нагрузки", "записей нагрузки")} без возможности отмены."),

            DeleteEntityKind.Bell => (
                $"Удалить {count} {Plural(count, "запись звонков", "записи звонков", "записей звонков")}?",
                $"Будут удалены {count} {Plural(count, "запись звонков", "записи звонков", "записей звонков")} без возможности отмены."),

            DeleteEntityKind.Template => (
                $"Удалить {count} {Plural(count, "шаблон", "шаблона", "шаблонов")}?",
                $"Будут удалены {count} {Plural(count, "шаблон", "шаблона", "шаблонов")} без возможности отмены."),

            DeleteEntityKind.Period => (
                $"Удалить {count} {Plural(count, "период", "периода", "периодов")}?",
                $"Будут удалены {count} {Plural(count, "период", "периода", "периодов")} без возможности отмены."),

            DeleteEntityKind.CalendarEntry => (
                $"Удалить {count} {Plural(count, "запись календаря", "записи календаря", "записей календаря")}?",
                $"Будут удалены {count} {Plural(count, "запись календаря", "записи календаря", "записей календаря")} без возможности отмены."),

            _ => (
                $"Удалить {count} {Plural(count, "запись", "записи", "записей")}?",
                $"Будут удалены {count} {Plural(count, "запись", "запись", "записей")} без возможности отмены.")
        };
    }

    private static string PersonDeletedVerb(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var patronymic = parts.Length >= 3 ? parts[2] : parts.LastOrDefault() ?? "";
        var lower = patronymic.ToLowerInvariant();

        if (lower.EndsWith("овна") || lower.EndsWith("евна") || lower.EndsWith("ична")
            || lower.EndsWith("inichna") || lower.EndsWith("кызы"))
            return "будет удалена";

        if (lower.EndsWith("ович") || lower.EndsWith("евич") || lower.EndsWith("ич")
            || lower.EndsWith("оглы") || lower.EndsWith("улы"))
            return "будет удалён";

        return "будет удалён";
    }

    private static string Plural(int count, string one, string few, string many)
    {
        var n = Math.Abs(count) % 100;
        var n1 = n % 10;
        if (n is > 10 and < 20)
            return many;
        if (n1 is 1)
            return one;
        if (n1 is >= 2 and <= 4)
            return few;
        return many;
    }
}
