namespace ArmZavuch.Models;

/// <summary>Учёт замены для отчётов: диспетчерская, ручной ввод.</summary>
public sealed class SubstitutionRecord
{
    public int Id { get; set; }
    public string Date { get; set; } = "";
    public int LessonNumber { get; set; }
    public int? ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int ClassShift { get; set; } = 1;
    public int? SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public int AbsentTeacherId { get; set; }
    public string AbsentTeacherName { get; set; } = "";
    public int ReplacementTeacherId { get; set; }
    public string ReplacementTeacherName { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public bool IsOfficial { get; set; } = true;
    public string Source { get; set; } = AbsenceSources.Dispatcher;
    public string? Note { get; set; }
    public int? DayOverrideId { get; set; }

    public string TimeDisplay =>
        string.IsNullOrWhiteSpace(StartTime)
            ? ""
            : string.IsNullOrWhiteSpace(EndTime)
                ? StartTime
                : $"{StartTime}–{EndTime}";

    public string OfficialLabel => IsOfficial ? "официально" : "неофициально";

    public string ShiftDisplay => ClassShift == 2 ? "2 смена" : "1 смена";

    public string DateDisplay =>
        DateOnly.TryParse(Date, out var d) ? d.ToString("dd.MM.yyyy") : Date;

    public string JournalHeadline =>
        string.IsNullOrWhiteSpace(TimeDisplay)
            ? $"{DateDisplay} · {ShiftDisplay} · урок {LessonNumber}"
            : $"{DateDisplay} · {ShiftDisplay} · урок {LessonNumber} · {TimeDisplay}";

    public string JournalContextLine
    {
        get
        {
            var parts = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(ClassName))
                parts.Add(ClassName);
            if (!string.IsNullOrWhiteSpace(SubjectName))
                parts.Add(SubjectName);
            return parts.Count > 0 ? string.Join(" · ", parts) : "—";
        }
    }

    public string JournalSubstitutionLine =>
        $"{AbsentTeacherName} → {ReplacementTeacherName}";

    public string JournalMetaLine => OfficialLabel;

    public string JournalRowLine =>
        $"{JournalSubstitutionLine} · {JournalContextLine} · {JournalMetaLine}";

    public string DisplayLine =>
        $"{Date} · урок {LessonNumber} · {ClassName} · {SubjectName} · " +
        $"{AbsentTeacherName} → {ReplacementTeacherName} ({OfficialLabel})";
}
