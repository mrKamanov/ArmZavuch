namespace ArmZavuch.Services.Catalog;

public sealed record SubjectDifficultyChange(
    int SubjectId,
    string Name,
    double OldScore,
    double NewScore,
    string MatchedName,
    bool IsFuzzy);

public sealed record SubjectDifficultyRefreshResult(
    IReadOnlyList<SubjectDifficultyChange> Updated,
    int Unchanged,
    int Unmatched)
{
    public int UpdatedCount => Updated.Count;
}
