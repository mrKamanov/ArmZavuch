namespace ArmZavuch.Models;

/// <summary>Форматы и данные для drag-and-drop в Конструкторе.</summary>
public static class DragFormats
{
    public const string Cell = "ArmZavuch.GridCell";
    public const string Curriculum = "ArmZavuch.CurriculumItem";
    public const string Teacher = "ArmZavuch.Teacher";
    public const string Subject = "ArmZavuch.Subject";
    public const string Room = "ArmZavuch.Room";
    public const string DayEditorCell = "ArmZavuch.DayEditorCell";
}

/// <summary>Переносимая ячейка сетки.</summary>
public sealed class CellDragData
{
    public int ClassId { get; init; }
    public int LessonNumber { get; init; }
    public int? SlotId { get; init; }
    public int? SubjectId { get; init; }
    public int? TeacherId { get; init; }
    public int? RoomId { get; init; }
    public int SubgroupIndex { get; init; }
    public int DayOfWeek { get; init; }
}

/// <summary>Переносимая строка нагрузки из палитры.</summary>
public sealed class CurriculumDragData
{
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = "";
    public bool HasSubgroups { get; init; }
}

public sealed class TeacherDragData
{
    public int TeacherId { get; init; }
    public string TeacherName { get; init; } = "";
}

public sealed class SubjectDragData
{
    public int ClassId { get; init; }
    public string ClassName { get; init; } = "";
    public int SubjectId { get; init; }
    public string SubjectName { get; init; } = "";
    public double DifficultyScore { get; init; }
    public bool HasSubgroups { get; init; }
}

public sealed class RoomDragData
{
    public int RoomId { get; init; }
    public string RoomNumber { get; init; } = "";
    public string BuildingName { get; init; } = "";
}

/// <summary>Переносимый урок в конструкторе дня (оперативная перестановка).</summary>
public sealed class DayEditorCellDragData
{
    public int ClassId { get; init; }
    public int LessonNumber { get; init; }
    public int? SlotId { get; init; }
}
