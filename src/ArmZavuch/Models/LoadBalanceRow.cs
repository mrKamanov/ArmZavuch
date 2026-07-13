namespace ArmZavuch.Models;

/// <summary>Сверка нагрузки из справочника и разложения в сетке недельного шаблона (ТЗ §4).</summary>
public sealed class LoadBalanceRow
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public int ClassId { get; set; }
    public int SubjectId { get; set; }
    public double PlannedHours { get; set; }
    public double ScheduledHours { get; set; }
    public double Delta => ScheduledHours - PlannedHours;
    public bool HasWarning => Math.Abs(Delta) > 0.01;
    public bool IsExtraInGrid => PlannedHours < 0.01 && ScheduledHours > 0.01;

    public string HoursLine => $"нагрузка {PlannedHours:0.#} · сетка {ScheduledHours:0.#}";

    public string DeltaLine => !HasWarning
        ? ""
        : IsExtraInGrid
            ? $"+{ScheduledHours:0.#} ч лишних в сетке"
            : Delta > 0
                ? $"+{Delta:0.#} ч в сетке"
                : $"{Math.Abs(Delta):0.#} ч не хватает в сетке";

    public string NavigationHint => IsExtraInGrid
        ? "Перейти в сетку"
        : "Перейти в нагрузку";
}
