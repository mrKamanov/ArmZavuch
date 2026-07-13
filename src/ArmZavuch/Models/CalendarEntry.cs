namespace ArmZavuch.Models;

/// <summary>Исключение учебного календаря (ТЗ §3).</summary>
public sealed class CalendarEntry : SelectableEntity
{
    public int Id { get; set; }
    public string StartDate { get; set; } = "";
    public string? EndDate { get; set; }
    public string ExceptionType { get; set; } = CalendarExceptionTypes.Holiday;
    public int? DonorDayOfWeek { get; set; }
    public string? Note { get; set; }

    public string TypeDisplay => CalendarExceptionTypes.ToDisplay(ExceptionType);
}
