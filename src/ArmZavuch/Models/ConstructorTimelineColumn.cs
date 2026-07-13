namespace ArmZavuch.Models;

/// <summary>Столбец сетки Конструктора: урок или дин. пауза между уроками.</summary>
public sealed class ConstructorTimelineColumn
{
    public bool IsDynamicPause { get; init; }
    public bool IsBreak { get; init; }
    /// <summary>Номер урока в шаблоне звонков (для колонки «Урок N»).</summary>
    public int LessonNumber { get; init; }
    /// <summary>Номер урока в bell_periods (для поиска времени звонка).</summary>
    public int BellLessonNumber { get; init; }
    /// <summary>После какого урока дин. пауза (только для паузы).</summary>
    public int AfterLessonNumber { get; init; }
    /// <summary>Номер урока в БД для сохранения слота.</summary>
    public int StorageLessonNumber { get; init; }
    public string Title { get; init; } = "";
    public string BellTimeDisplay { get; init; } = "";
    public bool HasBellTime => !string.IsNullOrWhiteSpace(BellTimeDisplay);
}
