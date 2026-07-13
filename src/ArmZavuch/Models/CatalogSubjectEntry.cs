namespace ArmZavuch.Models;

using ArmZavuch.Data;

/// <summary>Запись типового перечня предметов (ФГОС РФ).</summary>
public sealed class CatalogSubjectEntry
{
    public string Name { get; init; } = "";
    public int GradeFrom { get; init; } = 1;
    public int GradeTo { get; init; } = 11;
    public double DefaultDifficulty { get; init; } = 1.0;
    public string Kind { get; init; } = SubjectKinds.Academic;

    public string GradeRangeDisplay => GradeFrom == GradeTo
        ? $"{GradeFrom} кл."
        : $"{GradeFrom}–{GradeTo} кл.";

    public string KindBadge
    {
        get
        {
            var b = SubjectKinds.Badge(Kind);
            return string.IsNullOrEmpty(b) ? "" : $"[{b}] ";
        }
    }

    public string DisplayLine =>
        $"{KindBadge}{Name} ({GradeRangeDisplay}, Сивков {DifficultyDisplay})";

    private string DifficultyDisplay =>
        OfficialSubjectDifficultyReference.FormatScoreRange(Name)
        ?? OfficialSubjectDifficultyReference.FormatScore(DefaultDifficulty);

    public bool AppliesToGrade(int grade) => grade >= GradeFrom && grade <= GradeTo;
}
