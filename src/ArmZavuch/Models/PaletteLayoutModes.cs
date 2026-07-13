namespace ArmZavuch.Models;

/// <summary>Режимы группировки и сортировки палитры «Нагрузка» в Конструкторе.</summary>
public static class PaletteGroupModes
{
    public const string ByClass = "Class";
    public const string BySubject = "Subject";
    public const string ByGrade = "Grade";
    public const string None = "None";
}

public static class PaletteSortModes
{
    public const string ClassThenSubject = "ClassSubject";
    public const string SubjectThenClass = "SubjectClass";
    public const string HoursDesc = "HoursDesc";
    public const string HoursAsc = "HoursAsc";
}
