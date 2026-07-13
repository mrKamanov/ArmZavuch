using ArmZavuch.Data;
using ArmZavuch.Data.Repositories;
using ArmZavuch.Models;
using ArmZavuch.Services.Text;

namespace ArmZavuch.Services.Catalog;

/// <summary>Справочник типовых предметов РФ: поиск, фильтр по классу, импорт в БД.</summary>
public sealed class SubjectCatalogService
{
    private readonly SubjectRepository _subjects;
    private readonly TextSuggestionService _suggestions;

    public SubjectCatalogService(SubjectRepository subjects, TextSuggestionService suggestions)
    {
        _subjects = subjects;
        _suggestions = suggestions;
    }

    public IReadOnlyList<CatalogSubjectEntry> Search(string? query, int? grade)
    {
        var q = query?.Trim() ?? "";
        return RussianSubjectCatalog.All
            .Where(e => grade is null || e.AppliesToGrade(grade.Value))
            .Where(e => q.Length == 0 || e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.GradeFrom)
            .ThenBy(e => e.Name)
            .ToList();
    }

    public IEnumerable<string> GetAllKnownNames()
    {
        foreach (var e in RussianSubjectCatalog.All)
            yield return e.Name;
    }

    public TextSuggestion? SuggestSubjectName(string input, IEnumerable<Subject> existing) =>
        _suggestions.SuggestTitle(input, GetAllKnownNames().Concat(existing.Select(s => s.Name)));

    public SubjectDifficultyMatch? MatchDifficulty(string? rawName, int? grade = null)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return null;

        var dictionary = grade is int g
            ? OfficialSubjectDifficultyReference.GetLookupNamesForGrade(g)
                .Concat(GetAllKnownNames())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : OfficialSubjectDifficultyReference.AllLookupNames
                .Concat(GetAllKnownNames())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var variant in SubjectNameNormalizer.LookupVariants(rawName))
        {
            if (grade is int gr && OfficialSubjectDifficultyReference.TryGet(variant, gr, out var gradeScore))
                return new SubjectDifficultyMatch(gradeScore, variant, false, gr);

            if (grade is null && OfficialSubjectDifficultyReference.TryGet(variant, out var anyScore))
                return new SubjectDifficultyMatch(anyScore, variant, false);
        }

        foreach (var variant in SubjectNameNormalizer.LookupVariants(rawName))
        {
            var closest = _suggestions.FindClosestMatch(variant, dictionary);
            if (closest is null)
                continue;

            if (grade is int gr && OfficialSubjectDifficultyReference.TryGet(closest, gr, out var gradeScore))
            {
                var fuzzy = !closest.Equals(variant, StringComparison.OrdinalIgnoreCase);
                return new SubjectDifficultyMatch(gradeScore, closest, fuzzy, gr);
            }

            if (grade is null && OfficialSubjectDifficultyReference.TryGet(closest, out var anyScore))
            {
                var fuzzy = !closest.Equals(variant, StringComparison.OrdinalIgnoreCase);
                return new SubjectDifficultyMatch(anyScore, closest, fuzzy);
            }
        }

        return null;
    }

    public double? SuggestDifficulty(string? rawName, int? grade = null) =>
        MatchDifficulty(rawName, grade)?.Score;

    public double ResolveDifficulty(string? rawName, int? grade = null, double? explicitScore = null) =>
        explicitScore ?? SuggestDifficulty(rawName, grade) ?? OfficialSubjectDifficultyReference.DefaultFallback;

    public async Task<SubjectDifficultyRefreshResult> RefreshDifficultiesAsync(
        IReadOnlyList<Subject> subjects,
        int? referenceGrade = null)
    {
        var updated = new List<SubjectDifficultyChange>();
        var unchanged = 0;
        var unmatched = 0;

        foreach (var subject in subjects)
        {
            var grade = referenceGrade ?? ResolveReferenceGrade(subject.Name);
            var match = MatchDifficulty(subject.Name, grade);
            if (match is null)
            {
                unmatched++;
                continue;
            }

            if (NearlyEqual(subject.DifficultyScore, match.Score))
            {
                unchanged++;
                continue;
            }

            var oldScore = subject.DifficultyScore;
            subject.DifficultyScore = match.Score;
            await _subjects.UpdateAsync(subject);
            updated.Add(new SubjectDifficultyChange(
                subject.Id,
                subject.Name,
                oldScore,
                match.Score,
                match.MatchedName,
                match.IsFuzzy));
        }

        return new SubjectDifficultyRefreshResult(updated, unchanged, unmatched);
    }

    public int ResolveReferenceGrade(string subjectName)
    {
        var normalized = subjectName.Trim();
        var catalog = RussianSubjectCatalog.All
            .FirstOrDefault(e => e.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (catalog is not null)
            return catalog.GradeFrom;

        var closest = _suggestions.FindClosestMatch(normalized, GetAllKnownNames());
        if (closest is not null)
        {
            catalog = RussianSubjectCatalog.All
                .FirstOrDefault(e => e.Name.Equals(closest, StringComparison.OrdinalIgnoreCase));
            if (catalog is not null)
                return catalog.GradeFrom;
        }

        foreach (var variant in SubjectNameNormalizer.LookupVariants(normalized))
        {
            catalog = RussianSubjectCatalog.All
                .FirstOrDefault(e => e.Name.Equals(variant, StringComparison.OrdinalIgnoreCase));
            if (catalog is not null)
                return catalog.GradeFrom;
        }

        return 5;
    }

    private static bool NearlyEqual(double a, double b) =>
        Math.Abs(a - b) < 0.001;

    public async Task<int> ImportEntriesAsync(IEnumerable<CatalogSubjectEntry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (await _subjects.FindIdByNameAsync(entry.Name) is not null)
                continue;
            await _subjects.InsertAsync(new Subject
            {
                Name = entry.Name,
                DifficultyScore = entry.DefaultDifficulty
            });
            count++;
        }
        return count;
    }

    public Task<int> ImportForGradeAsync(int grade) =>
        ImportEntriesAsync(RussianSubjectCatalog.All.Where(e => e.AppliesToGrade(grade)));

    public async Task<Subject> ResolveOrCreateAsync(string rawName, IEnumerable<Subject> existing, int? grade = null)
    {
        var trimmed = rawName.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Пустое название предмета");

        var name = ProperNameFormatter.FormatTitle(trimmed);

        if (await _subjects.FindIdByNameAsync(name) is int id)
            return await _subjects.GetByIdAsync(id) ?? new Subject { Id = id, Name = name };

        var newId = await _subjects.InsertAsync(new Subject
        {
            Name = name,
            DifficultyScore = ResolveDifficulty(name, grade)
        });
        return new Subject { Id = newId, Name = name };
    }
}
