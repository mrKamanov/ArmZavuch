namespace ArmZavuch.Services.Catalog;

public sealed record SubjectDifficultyMatch(double Score, string MatchedName, bool IsFuzzy, int? Grade = null)
{
    public string Hint => Grade is int g
        ? IsFuzzy
            ? $"Балл как у «{MatchedName}» для {g} кл. (похожее название)"
            : $"Балл для «{MatchedName}», {g} кл."
        : IsFuzzy
            ? $"Балл как у «{MatchedName}» (похожее название)"
            : $"Балл для «{MatchedName}»";
}
