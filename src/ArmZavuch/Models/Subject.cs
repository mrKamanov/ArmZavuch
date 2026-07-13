namespace ArmZavuch.Models;

using ArmZavuch.Data;

/// <summary>Предмет и балл трудности по Сивкову (ТЗ §3).</summary>
public sealed class Subject : SelectableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double DifficultyScore { get; set; } = OfficialSubjectDifficultyReference.DefaultFallback;

    /// <summary>Диапазон официальных баллов по параллелям (шаблон Минпросвещения) или сохранённое значение.</summary>
    public string DifficultyCatalogRange =>
        OfficialSubjectDifficultyReference.FormatScoreRange(Name)
        ?? OfficialSubjectDifficultyReference.FormatScore(DifficultyScore);
}
