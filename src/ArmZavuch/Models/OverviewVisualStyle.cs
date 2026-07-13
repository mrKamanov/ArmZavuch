namespace ArmZavuch.Models;

/// <summary>Подсветка колонок дней и разделителей строк в сводке расписания.</summary>
public static class OverviewVisualStyle
{
    public static string DayColumnBackground(int dayOfWeek) =>
        dayOfWeek % 2 == 1 ? "#FFFFFF" : "#F0F4F8";

    public static string DayHeaderBackground(int dayOfWeek) =>
        dayOfWeek % 2 == 1 ? "#E8EEF4" : "#DDE6EF";

    public static string DayColumnBorder(int dayOfWeek) =>
        dayOfWeek % 2 == 1 ? "#D1D9E0" : "#B8C4D0";
}
