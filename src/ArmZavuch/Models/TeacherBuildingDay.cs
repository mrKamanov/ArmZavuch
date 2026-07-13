namespace ArmZavuch.Models;

/// <summary>В каком здании педагог работает в указанный день недели (для проверки).</summary>
public sealed class TeacherBuildingDay
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int DayOfWeek { get; set; }
    public int BuildingId { get; set; }
    public string BuildingName { get; set; } = "";

    public string DisplayText =>
        $"Каждую {DayName(DayOfWeek)}: «{BuildingName}»";

    private static string DayName(int dow) => dow switch
    {
        1 => "Пн",
        2 => "Вт",
        3 => "Ср",
        4 => "Чт",
        5 => "Пт",
        6 => "Сб",
        _ => "?"
    };
}
