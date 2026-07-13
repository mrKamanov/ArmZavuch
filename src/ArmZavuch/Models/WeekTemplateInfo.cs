namespace ArmZavuch.Models;

/// <summary>Недельный шаблон для выбора в Конструкторе.</summary>
public sealed class WeekTemplateInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string WeekParity { get; set; } = WeekTemplateParity.Any;

    public string DisplayName
    {
        get
        {
            if (WeekParity == WeekTemplateParity.Any)
                return Name;

            var parityLabel = WeekTemplateParity.ToDisplay(WeekParity);
            if (Name.Equals(parityLabel, StringComparison.OrdinalIgnoreCase))
                return Name;

            return $"{Name} ({parityLabel})";
        }
    }

    public string TabHint => WeekParity switch
    {
        WeekTemplateParity.WeekA =>
            "Неделя А — расписание для первой недели чередования (нечётные недели четверти).",
        WeekTemplateParity.WeekB =>
            "Неделя Б — расписание для второй недели чередования (чётные недели четверти).",
        _ => "Обычный шаблон — одно и то же расписание каждую неделю."
    };
}

