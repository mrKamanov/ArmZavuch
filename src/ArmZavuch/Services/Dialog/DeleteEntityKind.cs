namespace ArmZavuch.Services.Dialog;

/// <summary>Тип удаляемой сущности — для корректных формулировок на русском.</summary>
public enum DeleteEntityKind
{
    Generic,
    Teacher,
    Subject,
    Building,
    SchoolClass,
    Room,
    Curriculum,
    Bell,
    Template,
    Period,
    CalendarEntry
}
